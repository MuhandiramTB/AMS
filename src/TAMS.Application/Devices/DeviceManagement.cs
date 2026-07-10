using FluentValidation;
using MediatR;
using TAMS.Application.Common.Exceptions;
using TAMS.Application.Common.Ports;
using TAMS.Domain.Devices;

namespace TAMS.Application.Devices;

// --- Register a device (FR-ZK-010) ---
public sealed record RegisterDeviceCommand(string SerialNo, string Name, string? IpAddress, int? Port, string? Model)
    : IRequest<DeviceDto>;

public sealed class RegisterDeviceValidator : AbstractValidator<RegisterDeviceCommand>
{
    public RegisterDeviceValidator()
    {
        RuleFor(x => x.SerialNo).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}

public sealed class RegisterDeviceHandler : IRequestHandler<RegisterDeviceCommand, DeviceDto>
{
    private readonly IDeviceRepository _devices;
    private readonly IUnitOfWork _unitOfWork;

    public RegisterDeviceHandler(IDeviceRepository devices, IUnitOfWork unitOfWork)
    {
        _devices = devices;
        _unitOfWork = unitOfWork;
    }

    public async Task<DeviceDto> Handle(RegisterDeviceCommand request, CancellationToken cancellationToken)
    {
        if (await _devices.SerialExistsAsync(request.SerialNo, cancellationToken))
        {
            throw new ConflictException($"A device with serial '{request.SerialNo}' is already registered.");
        }

        var device = new Device(request.SerialNo, request.Name, request.IpAddress, request.Port, request.Model);
        await _devices.AddAsync(device, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return DeviceDto.FromEntity(device);
    }
}

// --- List devices ---
public sealed record GetDevicesQuery : IRequest<IReadOnlyList<DeviceDto>>;

public sealed class GetDevicesHandler : IRequestHandler<GetDevicesQuery, IReadOnlyList<DeviceDto>>
{
    private readonly IDeviceRepository _devices;
    public GetDevicesHandler(IDeviceRepository devices) => _devices = devices;

    public async Task<IReadOnlyList<DeviceDto>> Handle(GetDevicesQuery request, CancellationToken cancellationToken)
    {
        var devices = await _devices.GetAllAsync(cancellationToken);
        return devices.Select(DeviceDto.FromEntity).ToList();
    }
}

// --- Test connection (FR-ZK-010) ---
public sealed record TestDeviceConnectionResult(bool Reachable, string? Message);

public sealed record TestDeviceConnectionCommand(long DeviceId) : IRequest<TestDeviceConnectionResult>;

public sealed class TestDeviceConnectionHandler : IRequestHandler<TestDeviceConnectionCommand, TestDeviceConnectionResult>
{
    private readonly IDeviceRepository _devices;
    private readonly IDeviceGateway _gateway;

    public TestDeviceConnectionHandler(IDeviceRepository devices, IDeviceGateway gateway)
    {
        _devices = devices;
        _gateway = gateway;
    }

    public async Task<TestDeviceConnectionResult> Handle(TestDeviceConnectionCommand request, CancellationToken cancellationToken)
    {
        var device = await _devices.GetByIdAsync(request.DeviceId, cancellationToken)
            ?? throw new NotFoundException("Device", request.DeviceId);

        var probe = await _gateway.TestConnectionAsync(
            new DeviceConnection(device.Id, device.SerialNo, device.IpAddress, device.Port), cancellationToken);
        return new TestDeviceConnectionResult(probe.Reachable, probe.Message);
    }
}

// --- Get sync state (FR-ZK-011 visibility) ---
public sealed record GetDeviceSyncStateQuery(long DeviceId) : IRequest<DeviceSyncStateDto>;

public sealed class GetDeviceSyncStateHandler : IRequestHandler<GetDeviceSyncStateQuery, DeviceSyncStateDto>
{
    private readonly IDeviceRepository _devices;
    public GetDeviceSyncStateHandler(IDeviceRepository devices) => _devices = devices;

    public async Task<DeviceSyncStateDto> Handle(GetDeviceSyncStateQuery request, CancellationToken cancellationToken)
    {
        var state = await _devices.GetSyncStateAsync(request.DeviceId, cancellationToken)
            ?? new DeviceSyncState(request.DeviceId); // never-synced → zeroed state
        return DeviceSyncStateDto.FromEntity(state);
    }
}
