using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TAMS.Domain.Leave;

namespace TAMS.Infrastructure.Persistence.Configurations;

/// <summary>Maps Leave.LeaveType per 04 §6.6.</summary>
public sealed class LeaveTypeConfiguration : IEntityTypeConfiguration<LeaveType>
{
    public void Configure(EntityTypeBuilder<LeaveType> builder)
    {
        builder.ToTable("LeaveType", "Leave");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(32).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("UQ_LeaveType_Code");
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

/// <summary>Maps Leave.LeaveRequest per 04 §6.6.</summary>
public sealed class LeaveRequestConfiguration : IEntityTypeConfiguration<LeaveRequest>
{
    public void Configure(EntityTypeBuilder<LeaveRequest> builder)
    {
        builder.ToTable("LeaveRequest", "Leave", t =>
            t.HasCheckConstraint("CK_Leave_EndAfterStart", "[EndDate] >= [StartDate]"));

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasConversion<byte>();
        builder.Property(x => x.Reason).HasMaxLength(500);
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasOne<LeaveType>().WithMany().HasForeignKey(x => x.LeaveTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.EmployeeId, x.Status }).HasDatabaseName("IX_Leave_Employee_Status");
        builder.HasIndex(x => new { x.EmployeeId, x.StartDate, x.EndDate });

        builder.Ignore(x => x.DayCount);
    }
}

/// <summary>Maps Leave.LeaveBalance per 04 §6.6.</summary>
public sealed class LeaveBalanceConfiguration : IEntityTypeConfiguration<LeaveBalance>
{
    public void Configure(EntityTypeBuilder<LeaveBalance> builder)
    {
        builder.ToTable("LeaveBalance", "Leave", t =>
            t.HasCheckConstraint("CK_LeaveBalance_UsedLEEntitled_OrOverride",
                "[UsedDays] >= 0")); // over-balance is allowed only via explicit override in code

        builder.HasKey(x => x.Id);
        builder.Property(x => x.EntitledDays).HasColumnType("decimal(6,2)");
        builder.Property(x => x.UsedDays).HasColumnType("decimal(6,2)");
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasOne<LeaveType>().WithMany().HasForeignKey(x => x.LeaveTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.EmployeeId, x.LeaveTypeId, x.Year })
            .IsUnique().HasDatabaseName("UQ_LeaveBalance_Emp_Type_Year");

        builder.Ignore(x => x.RemainingDays);
    }
}
