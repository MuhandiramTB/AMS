using Microsoft.EntityFrameworkCore;
using TAMS.Application.Common.Ports;
using TAMS.Domain.Devices;

namespace TAMS.Infrastructure.Persistence.Repositories;

public sealed class DeviceRepository : IDeviceRepository
{
    private readonly TamsDbContext _db;

    public DeviceRepository(TamsDbContext db) => _db = db;

    public Task<Device?> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
        _db.Devices.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public Task<bool> SerialExistsAsync(string serialNo, CancellationToken cancellationToken = default) =>
        _db.Devices.AnyAsync(d => d.SerialNo == serialNo, cancellationToken);

    public async Task<IReadOnlyList<Device>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _db.Devices.AsNoTracking().OrderBy(d => d.Name).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Device>> GetEnabledAsync(CancellationToken cancellationToken = default) =>
        await _db.Devices.Where(d => d.IsEnabled).ToListAsync(cancellationToken);

    public async Task AddAsync(Device device, CancellationToken cancellationToken = default) =>
        await _db.Devices.AddAsync(device, cancellationToken);

    public Task<DeviceSyncState?> GetSyncStateAsync(long deviceId, CancellationToken cancellationToken = default) =>
        _db.DeviceSyncStates.FirstOrDefaultAsync(s => s.DeviceId == deviceId, cancellationToken);

    public async Task AddSyncStateAsync(DeviceSyncState state, CancellationToken cancellationToken = default) =>
        await _db.DeviceSyncStates.AddAsync(state, cancellationToken);

    public async Task AddEventLogAsync(DeviceEventLog log, CancellationToken cancellationToken = default) =>
        await _db.DeviceEventLogs.AddAsync(log, cancellationToken);

    public Task<bool> EnrollmentExistsAsync(long deviceId, string deviceUserId, CancellationToken cancellationToken = default) =>
        _db.EmployeeDeviceEnrollments.AnyAsync(
            e => e.DeviceId == deviceId && e.DeviceUserId == deviceUserId, cancellationToken);

    public async Task AddEnrollmentAsync(EmployeeDeviceEnrollment enrollment, CancellationToken cancellationToken = default) =>
        await _db.EmployeeDeviceEnrollments.AddAsync(enrollment, cancellationToken);

    public async Task<IReadOnlyList<EmployeeDeviceEnrollment>> GetEnrollmentsForEmployeeAsync(
        long employeeId, CancellationToken cancellationToken = default) =>
        await _db.EmployeeDeviceEnrollments.AsNoTracking()
            .Where(e => e.EmployeeId == employeeId).ToListAsync(cancellationToken);

    public async Task<long?> ResolveEmployeeIdAsync(long deviceId, string deviceUserId, CancellationToken cancellationToken = default)
    {
        var enrollment = await _db.EmployeeDeviceEnrollments.AsNoTracking()
            .FirstOrDefaultAsync(e => e.DeviceId == deviceId && e.DeviceUserId == deviceUserId && e.IsActive, cancellationToken);
        return enrollment?.EmployeeId;
    }
}
