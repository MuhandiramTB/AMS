using Microsoft.EntityFrameworkCore;
using TAMS.Application.Common.Ports;
using TAMS.Domain.Attendance;
using TAMS.Domain.Audit;
using TAMS.Domain.Devices;
using TAMS.Domain.Identity;
using TAMS.Domain.Scheduling;
using TAMS.Domain.Workforce;

namespace TAMS.Infrastructure.Persistence;

/// <summary>
/// EF Core context for TAMS. Lives only in Infrastructure; the Application layer
/// depends on IUnitOfWork/repository ports, never on this type. (07 §4.2.)
///
/// SaveChanges is overridden to persist business data and its audit trail in a
/// SINGLE transaction (04 §10, BRULE-10): business rows are saved first so
/// server-generated Ids exist, then the audit rows are written and saved, and the
/// whole thing commits atomically. If either step fails, nothing commits — there
/// is never a committed change without its audit entry.
/// </summary>
public sealed class TamsDbContext : DbContext, IUnitOfWork
{
    private readonly AuditTrailBuilder? _auditTrailBuilder;

    public TamsDbContext(DbContextOptions<TamsDbContext> options) : base(options)
    {
    }

    public TamsDbContext(DbContextOptions<TamsDbContext> options, AuditTrailBuilder auditTrailBuilder)
        : base(options)
    {
        _auditTrailBuilder = auditTrailBuilder;
    }

    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<ShiftAssignment> ShiftAssignments => Set<ShiftAssignment>();
    public DbSet<PunchTransaction> Punches => Set<PunchTransaction>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<DeviceSyncState> DeviceSyncStates => Set<DeviceSyncState>();
    public DbSet<DeviceEventLog> DeviceEventLogs => Set<DeviceEventLog>();
    public DbSet<EmployeeDeviceEnrollment> EmployeeDeviceEnrollments => Set<EmployeeDeviceEnrollment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TamsDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // No audit builder (e.g. design-time factory) → plain save.
        if (_auditTrailBuilder is null)
        {
            return await base.SaveChangesAsync(cancellationToken);
        }

        var captured = _auditTrailBuilder.StampAndCapture(this);

        // Nothing to audit → a single ordinary save is atomic on its own.
        if (captured.Count == 0)
        {
            return await base.SaveChangesAsync(cancellationToken);
        }

        // Business data + audit rows must commit together. Use the execution
        // strategy (retries are enabled) around one explicit transaction.
        var strategy = Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await Database.BeginTransactionAsync(cancellationToken);

            var affected = await base.SaveChangesAsync(cancellationToken); // assigns Ids
            _auditTrailBuilder.WriteAuditEntries(this, captured);
            await base.SaveChangesAsync(cancellationToken);                // audit rows

            await transaction.CommitAsync(cancellationToken);
            return affected;
        });
    }

    public override int SaveChanges()
        => SaveChangesAsync().GetAwaiter().GetResult();
}
