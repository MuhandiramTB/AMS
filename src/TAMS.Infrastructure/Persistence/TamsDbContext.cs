using Microsoft.EntityFrameworkCore;
using TAMS.Application.Common.Ports;
using TAMS.Domain.Attendance;
using TAMS.Domain.Audit;
using TAMS.Domain.Devices;
using TAMS.Domain.Identity;
using TAMS.Domain.Leave;
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
    public DbSet<LeaveType> LeaveTypes => Set<LeaveType>();
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();
    public DbSet<LeaveBalance> LeaveBalances => Set<LeaveBalance>();

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

        // Peek at whether there is anything to audit without mutating state yet;
        // if not, a single ordinary save is atomic on its own.
        var hasAuditableChanges = ChangeTracker.Entries()
            .Any(e => e.Entity is TAMS.Domain.Common.Entity and not TAMS.Domain.Audit.AuditEntry
                && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted);

        if (!hasAuditableChanges)
        {
            return await base.SaveChangesAsync(cancellationToken);
        }

        // Business data + audit rows must commit together. Retries are enabled
        // (EnableRetryOnFailure), so the execution strategy may re-invoke this
        // delegate. It MUST be idempotent across attempts: we (1) drop any audit
        // rows a prior aborted attempt left tracked as Added, (2) re-stamp/-capture
        // from the live change tracker each attempt, so a rolled-back attempt
        // cannot double-write the audit trail or miscount affected rows. (BRULE-10.)
        var strategy = Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            DiscardTrackedAuditEntries();

            var captured = _auditTrailBuilder.StampAndCapture(this);

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

    /// <summary>
    /// Detaches any AuditEntry instances still tracked as Added — i.e. rows written
    /// by an earlier transaction attempt that was rolled back — so a retry starts
    /// from a clean slate and cannot duplicate the audit trail.
    /// </summary>
    private void DiscardTrackedAuditEntries()
    {
        var stale = ChangeTracker.Entries<AuditEntry>()
            .Where(e => e.State == EntityState.Added)
            .ToList();

        foreach (var entry in stale)
        {
            entry.State = EntityState.Detached;
        }
    }
}
