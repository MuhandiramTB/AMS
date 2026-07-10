using FluentValidation;
using MediatR;
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
    private readonly IUnitOfWork _unitOfWork;

    public EnrollEmployeeHandler(IDeviceRepository devices, IEmployeeRepository employees, IUnitOfWork unitOfWork)
    {
        _devices = devices;
        _employees = employees;
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

        // BRULE-09: a device slot maps to exactly one employee.
        if (await _devices.EnrollmentExistsAsync(request.DeviceId, request.DeviceUserId, cancellationToken))
        {
            throw new ConflictException(
                $"Device user id '{request.DeviceUserId}' is already enrolled on device {request.DeviceId}.");
        }

        var enrollment = new EmployeeDeviceEnrollment(request.EmployeeId, request.DeviceId, request.DeviceUserId);
        await _devices.AddEnrollmentAsync(enrollment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return EnrollmentDto.FromEntity(enrollment);
    }
}
