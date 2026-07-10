using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TAMS.Domain.Scheduling;

namespace TAMS.Infrastructure.Persistence.Configurations;

/// <summary>Maps Scheduling.Shift per 04 §6.3.</summary>
public sealed class ShiftConfiguration : IEntityTypeConfiguration<Shift>
{
    public void Configure(EntityTypeBuilder<Shift> builder)
    {
        builder.ToTable("Shift", "Scheduling");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).HasMaxLength(32).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("UQ_Shift_Code");
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.StartTime);
        builder.Property(x => x.EndTime);
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.Ignore(x => x.IsOvernight);
        builder.Ignore(x => x.ScheduledMinutes);
    }
}

/// <summary>Maps Scheduling.ShiftAssignment per 04 §6.3.</summary>
public sealed class ShiftAssignmentConfiguration : IEntityTypeConfiguration<ShiftAssignment>
{
    public void Configure(EntityTypeBuilder<ShiftAssignment> builder)
    {
        builder.ToTable("ShiftAssignment", "Scheduling", t =>
            t.HasCheckConstraint(
                "CK_ShiftAssign_OneTarget",
                "(([EmployeeId] IS NOT NULL AND [DepartmentId] IS NULL) OR ([EmployeeId] IS NULL AND [DepartmentId] IS NOT NULL))"));

        builder.HasKey(x => x.Id);

        builder.HasOne<Shift>().WithMany().HasForeignKey(x => x.ShiftId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.EmployeeId, x.EffectiveFrom }).HasDatabaseName("IX_ShiftAssign_Emp_Effective");
        builder.HasIndex(x => new { x.DepartmentId, x.EffectiveFrom });
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}
