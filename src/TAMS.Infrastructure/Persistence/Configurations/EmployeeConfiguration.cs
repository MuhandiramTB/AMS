using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TAMS.Domain.Workforce;

namespace TAMS.Infrastructure.Persistence.Configurations;

/// <summary>Maps Workforce.Employee and its status history per 04 §6.2.</summary>
public sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("Employee", "Workforce");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.EmployeeNo).HasMaxLength(32).IsRequired();
        builder.HasIndex(x => x.EmployeeNo).IsUnique().HasDatabaseName("UQ_Employee_EmployeeNo");

        builder.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.LastName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(256);
        builder.Property(x => x.Status).HasConversion<byte>();
        builder.Property(x => x.IsActive).HasDefaultValue(true);

        builder.HasOne<Department>()
            .WithMany()
            .HasForeignKey(x => x.PrimaryDepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.PrimaryDepartmentId);

        // Status history owned collection (insert-only).
        builder.HasMany(x => x.StatusHistory)
            .WithOne()
            .HasForeignKey(h => h.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.StatusHistory).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

/// <summary>Maps Workforce.EmployeeStatusHistory per 04 §6.2.</summary>
public sealed class EmployeeStatusHistoryConfiguration : IEntityTypeConfiguration<EmployeeStatusHistory>
{
    public void Configure(EntityTypeBuilder<EmployeeStatusHistory> builder)
    {
        builder.ToTable("EmployeeStatusHistory", "Workforce");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasConversion<byte>();
        builder.Property(x => x.Reason).HasMaxLength(200);
    }
}
