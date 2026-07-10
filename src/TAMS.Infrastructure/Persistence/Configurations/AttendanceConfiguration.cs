using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TAMS.Domain.Attendance;

namespace TAMS.Infrastructure.Persistence.Configurations;

/// <summary>Maps Attendance.PunchTransaction (immutable, insert-only) per 04 §6.5.</summary>
public sealed class PunchTransactionConfiguration : IEntityTypeConfiguration<PunchTransaction>
{
    public void Configure(EntityTypeBuilder<PunchTransaction> builder)
    {
        builder.ToTable("PunchTransaction", "Attendance");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.DeviceUserId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Direction).HasConversion<byte>();
        builder.Property(x => x.SourceType).HasConversion<byte>();

        builder.Property(x => x.IdempotencyKey).HasMaxLength(128).IsRequired();
        builder.HasIndex(x => x.IdempotencyKey).IsUnique().HasDatabaseName("UQ_Punch_IdempotencyKey");

        builder.HasIndex(x => new { x.DeviceId, x.PunchedAtUtc })
            .HasDatabaseName("IX_Punch_DeviceId_PunchedAtUtc");
        builder.HasIndex(x => new { x.EmployeeId, x.PunchedAtUtc })
            .HasDatabaseName("IX_Punch_EmployeeId_PunchedAtUtc");
    }
}

/// <summary>Maps Attendance.AttendanceRecord (+ owned exceptions/corrections) per 04 §6.5.</summary>
public sealed class AttendanceRecordConfiguration : IEntityTypeConfiguration<AttendanceRecord>
{
    public void Configure(EntityTypeBuilder<AttendanceRecord> builder)
    {
        builder.ToTable("AttendanceRecord", "Attendance");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status).HasConversion<byte>();
        builder.HasIndex(x => new { x.EmployeeId, x.WorkDate })
            .IsUnique().HasDatabaseName("UQ_AttendanceRecord_Employee_WorkDate");
        builder.HasIndex(x => new { x.WorkDate, x.Status })
            .HasDatabaseName("IX_AttRec_WorkDate_Status");
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasMany(x => x.Exceptions).WithOne()
            .HasForeignKey(e => e.AttendanceRecordId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Exceptions).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(x => x.Corrections).WithOne()
            .HasForeignKey(c => c.AttendanceRecordId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Corrections).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class AttendanceExceptionConfiguration : IEntityTypeConfiguration<AttendanceException>
{
    public void Configure(EntityTypeBuilder<AttendanceException> builder)
    {
        builder.ToTable("AttendanceException", "Attendance");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExceptionType).HasConversion<byte>();
        builder.Property(x => x.Notes).HasMaxLength(1000);
        builder.HasIndex(x => x.IsResolved).HasFilter("[IsResolved] = 0").HasDatabaseName("IX_Exc_IsResolved");
    }
}

public sealed class AttendanceCorrectionConfiguration : IEntityTypeConfiguration<AttendanceCorrection>
{
    public void Configure(EntityTypeBuilder<AttendanceCorrection> builder)
    {
        builder.ToTable("AttendanceCorrection", "Attendance");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FieldName).HasMaxLength(64).IsRequired();
        builder.Property(x => x.OldValue).HasMaxLength(256);
        builder.Property(x => x.NewValue).HasMaxLength(256);
        builder.Property(x => x.Reason).HasMaxLength(500).IsRequired();
    }
}
