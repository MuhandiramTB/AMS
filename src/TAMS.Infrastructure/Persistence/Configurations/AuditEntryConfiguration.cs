using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TAMS.Domain.Audit;

namespace TAMS.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps Audit.AuditEntry per 04 §6.7. Append-only: no Modified*/RowVersion.
/// Update/Delete are additionally revoked at the DB-grant level in production
/// (04 §13); the app never issues them.
/// </summary>
public sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("AuditEntry", "Audit");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Action).HasMaxLength(128).IsRequired();
        builder.Property(x => x.EntityName).HasMaxLength(128).IsRequired();
        builder.Property(x => x.EntityId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.OccurredAtUtc).IsRequired();

        builder.HasIndex(x => new { x.EntityName, x.OccurredAtUtc })
            .HasDatabaseName("IX_Audit_Entity_OccurredAt");
    }
}
