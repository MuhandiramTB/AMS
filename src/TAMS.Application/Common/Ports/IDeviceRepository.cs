using TAMS.Domain.Devices;

namespace TAMS.Application.Common.Ports;

/// <summary>Persistence port for devices, their sync state, enrollments and event log.</summary>
public interface IDeviceRepository
{
    Task<Device?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<bool> SerialExistsAsync(string serialNo, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Device>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Device>> GetEnabledAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Device device, CancellationToken cancellationToken = default);

    Task<DeviceSyncState?> GetSyncStateAsync(long deviceId, CancellationToken cancellationToken = default);
    Task AddSyncStateAsync(DeviceSyncState state, CancellationToken cancellationToken = default);

    Task AddEventLogAsync(DeviceEventLog log, CancellationToken cancellationToken = default);

    // Enrollments
    Task<bool> EnrollmentExistsAsync(long deviceId, string deviceUserId, CancellationToken cancellationToken = default);
    Task AddEnrollmentAsync(EmployeeDeviceEnrollment enrollment, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EmployeeDeviceEnrollment>> GetEnrollmentsForEmployeeAsync(long employeeId, CancellationToken cancellationToken = default);

    /// <summary>Resolves the employee owning a (device, deviceUserId) pair, or null if unenrolled. (BRULE-09.)</summary>
    Task<long?> ResolveEmployeeIdAsync(long deviceId, string deviceUserId, CancellationToken cancellationToken = default);

    Task<EmployeeDeviceEnrollment?> GetEnrollmentByIdAsync(long enrollmentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EmployeeDeviceEnrollment>> GetEnrollmentsForDeviceAsync(long deviceId, CancellationToken cancellationToken = default);
}
