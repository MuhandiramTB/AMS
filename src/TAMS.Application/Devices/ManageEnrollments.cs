using MediatR;
using TAMS.Application.Common.Exceptions;
using TAMS.Application.Common.Ports;

namespace TAMS.Application.Devices;

// --- List a device's enrollments (FR-EMP-004 / FR-ZK-003 visibility) ---
public sealed record GetDeviceEnrollmentsQuery(long DeviceId) : IRequest<IReadOnlyList<EnrollmentDto>>;

public sealed class GetDeviceEnrollmentsHandler
    : IRequestHandler<GetDeviceEnrollmentsQuery, IReadOnlyList<EnrollmentDto>>
{
    private readonly IDeviceRepository _devices;
    public GetDeviceEnrollmentsHandler(IDeviceRepository devices) => _devices = devices;

    public async Task<IReadOnlyList<EnrollmentDto>> Handle(GetDeviceEnrollmentsQuery request, CancellationToken cancellationToken)
    {
        var enrollments = await _devices.GetEnrollmentsForDeviceAsync(request.DeviceId, cancellationToken);
        return enrollments.Select(EnrollmentDto.FromEntity).ToList();
    }
}

// --- Deactivate an enrollment (soft; frees the device slot for re-use) ---
public sealed record DeactivateEnrollmentCommand(long EnrollmentId) : IRequest;

public sealed class DeactivateEnrollmentHandler : IRequestHandler<DeactivateEnrollmentCommand>
{
    private readonly IDeviceRepository _devices;
    private readonly IUnitOfWork _unitOfWork;

    public DeactivateEnrollmentHandler(IDeviceRepository devices, IUnitOfWork unitOfWork)
    {
        _devices = devices;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(DeactivateEnrollmentCommand request, CancellationToken cancellationToken)
    {
        var enrollment = await _devices.GetEnrollmentByIdAsync(request.EnrollmentId, cancellationToken)
            ?? throw new NotFoundException("Enrollment", request.EnrollmentId);

        enrollment.Deactivate();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
