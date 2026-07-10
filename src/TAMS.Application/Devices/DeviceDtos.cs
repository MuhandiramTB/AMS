using TAMS.Domain.Devices;

namespace TAMS.Application.Devices;

public sealed record DeviceDto(
    long Id,
    string SerialNo,
    string Name,
    string? IpAddress,
    int? Port,
    string? Model,
    bool IsEnabled,
    DateTime? LastSeenUtc)
{
    public static DeviceDto FromEntity(Device d) =>
        new(d.Id, d.SerialNo, d.Name, d.IpAddress, d.Port, d.Model, d.IsEnabled, d.LastSeenUtc);
}

public sealed record DeviceSyncStateDto(
    long DeviceId,
    DateTime? LastWatermarkUtc,
    DateTime? LastSyncSucceededUtc,
    int ConsecutiveFailureCount)
{
    public static DeviceSyncStateDto FromEntity(DeviceSyncState s) =>
        new(s.DeviceId, s.LastWatermarkUtc, s.LastSyncSucceededUtc, s.ConsecutiveFailureCount);
}

public sealed record EnrollmentDto(long Id, long EmployeeId, long DeviceId, string DeviceUserId, bool IsActive)
{
    public static EnrollmentDto FromEntity(EmployeeDeviceEnrollment e) =>
        new(e.Id, e.EmployeeId, e.DeviceId, e.DeviceUserId, e.IsActive);
}
