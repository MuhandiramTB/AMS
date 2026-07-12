using FluentValidation;
using MediatR;
using TAMS.Application.Attendance;
using TAMS.Application.Common.Exceptions;
using TAMS.Application.Common.Ports;
using TAMS.Domain.Devices;

namespace TAMS.Application.Devices;

/// <summary>
/// Enrolls an employee on a device. Enforces the (device, deviceUserId) → single
/// employee uniqueness (BRULE-09) so punches always resolve to the right person.
/// (FR-EMP-004, FR-ZK-003.)
/// </summary>
public sealed record EnrollEmployeeCommand(long EmployeeId, long DeviceId, string DeviceUserId)
    : IRequest<EnrollmentDto>;

public sealed class EnrollEmployeeValidator : AbstractValidator<EnrollEmployeeCommand>
{
    public EnrollEmployeeValidator()
    {
        RuleFor(x => x.EmployeeId).GreaterThan(0);
        RuleFor(x => x.DeviceId).GreaterThan(0);
        RuleFor(x => x.DeviceUserId).NotEmpty().MaximumLength(64);
    }
}

public sealed class EnrollEmployeeHandler : IRequestHandler<EnrollEmployeeCommand, EnrollmentDto>
{
    private readonly IDeviceRepository _devices;
    private readonly IEmployeeRepository _employees;
    private readonly IAttendanceRepository _attendance;
    private readonly ISender _mediator;
    private readonly IUnitOfWork _unitOfWork;

    public EnrollEmployeeHandler(
        IDeviceRepository devices,
        IEmployeeRepository employees,
        IAttendanceRepository attendance,
        ISender mediator,
        IUnitOfWork unitOfWork)
    {
        _devices = devices;
        _employees = employees;
        _attendance = attendance;
        _mediator = mediator;
        _unitOfWork = unitOfWork;
    }

    public async Task<EnrollmentDto> Handle(EnrollEmployeeCommand request, CancellationToken cancellationToken)
    {
        if (await _employees.GetByIdAsync(request.EmployeeId, cancellationToken) is null)
        {
            throw new BusinessRuleException($"Employee '{request.EmployeeId}' does not exist.");
        }

        if (await _devices.GetByIdAsync(request.DeviceId, cancellationToken) is null)
        {
            throw new BusinessRuleException($"Device '{request.DeviceId}' does not exist.");
        }

        // BRULE-09: a device slot maps to exactly one employee. An ACTIVE slot is a
        // conflict; an INACTIVE slot (freed by a leaver) is re-used for the new hire —
        // reassigning the existing row rather than inserting a duplicate (the pair is
        // unique). This is what makes device-slot re-use work after staff turnover.
        var existing = await _devices.GetEnrollmentBySlotAsync(request.DeviceId, request.DeviceUserId, cancellationToken);
        EmployeeDeviceEnrollment enrollment;
        if (existing is not null)
        {
            if (existing.IsActive)
            {
                throw new ConflictException(
                    $"Device user id '{request.DeviceUserId}' is already enrolled on device {request.DeviceId}.");
            }
            existing.ReassignTo(request.EmployeeId);
            enrollment = existing;
        }
        else
        {
            enrollment = new EmployeeDeviceEnrollment(request.EmployeeId, request.DeviceId, request.DeviceUserId);
            await _devices.AddEnrollmentAsync(enrollment, cancellationToken);
        }

        // Back-fill any punches captured before this enrollment existed, so no
        // previously-unresolved punch is orphaned. (FR-ZK-003, BRULE-09.)
        var affectedDates = await _attendance.ResolveOrphanPunchesAsync(
            request.DeviceId, request.DeviceUserId, request.EmployeeId, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // (Re)process attendance for the recovered days so the punches enter records.
        foreach (var date in affectedDates)
        {
            await _mediator.Send(new ProcessAttendanceCommand(request.EmployeeId, date), cancellationToken);
        }

        return EnrollmentDto.FromEntity(enrollment);
    }
}
