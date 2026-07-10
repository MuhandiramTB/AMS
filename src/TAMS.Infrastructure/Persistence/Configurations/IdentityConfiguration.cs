using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TAMS.Domain.Identity;

namespace TAMS.Infrastructure.Persistence.Configurations;

/// <summary>Maps Identity.User (+ UserRole join) per 04 §6.1.</summary>
public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("User", "Identity");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserName).HasMaxLength(128).IsRequired();
        builder.HasIndex(x => x.UserName).IsUnique().HasDatabaseName("UQ_User_UserName");

        builder.Property(x => x.Email).HasMaxLength(256).IsRequired();
        builder.HasIndex(x => x.Email).IsUnique().HasDatabaseName("UQ_User_Email");

        builder.Property(x => x.PasswordHash).HasMaxLength(512).IsRequired();
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.Property(x => x.FailedLoginCount).HasDefaultValue(0);

        builder.HasMany(x => x.Roles)
            .WithMany()
            .UsingEntity(join => join.ToTable("UserRole", "Identity"));
        builder.Navigation(x => x.Roles).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

/// <summary>Maps Identity.Role (+ RolePermission join) per 04 §6.1.</summary>
public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Role", "Identity");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => x.Name).IsUnique().HasDatabaseName("UQ_Role_Name");
        builder.Property(x => x.Description).HasMaxLength(200);

        builder.HasMany(x => x.Permissions)
            .WithMany()
            .UsingEntity(join => join.ToTable("RolePermission", "Identity"));
        builder.Navigation(x => x.Permissions).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

/// <summary>Maps Identity.Permission per 04 §6.1.</summary>
public sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("Permission", "Identity");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(128).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("UQ_Permission_Code");
        builder.Property(x => x.Description).HasMaxLength(200);
    }
}

/// <summary>Maps Identity.RefreshToken per 04 §6.1. Stores token HASH only.</summary>
public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshToken", "Identity");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TokenHash).HasMaxLength(512).IsRequired();
        builder.HasIndex(x => x.TokenHash);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
