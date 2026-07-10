using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TAMS.Domain.Workforce;

namespace TAMS.Infrastructure.Persistence.Configurations;

/// <summary>Maps Workforce.Department per 04 §6.2.</summary>
public sealed class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.ToTable("Department", "Workforce");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).HasMaxLength(32).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("UQ_Department_Code");

        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.IsActive).HasDefaultValue(true);

        builder.HasOne<Department>()
            .WithMany()
            .HasForeignKey(x => x.ParentDepartmentId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(x => x.ParentDepartmentId);

        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}
