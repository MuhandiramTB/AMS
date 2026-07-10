using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TAMS.Domain.Devices;

namespace TAMS.Infrastructure.Persistence.Configurations;

/// <summary>Maps Devices.Device per 04 §6.4.</summary>
public sealed class DeviceConfiguration : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> builder)
    {
        builder.ToTable("Device", "Devices");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SerialNo).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => x.SerialNo).IsUnique().HasDatabaseName("UQ_Device_SerialNo");
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.IpAddress).HasMaxLength(64);
        builder.Property(x => x.Model).HasMaxLength(64);
        builder.Property(x => x.IsEnabled).HasDefaultValue(true);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}

/// <summary>Maps Devices.DeviceSyncState (1:1 with Device) per 04 §6.4.</summary>
public sealed class DeviceSyncStateConfiguration : IEntityTypeConfiguration<DeviceSyncState>
{
    public void Configure(EntityTypeBuilder<DeviceSyncState> builder)
    {
        builder.ToTable("DeviceSyncState", "Devices");
        builder.HasKey(x => x.DeviceId); // PK == FK (1:1)
        builder.Property(x => x.DeviceId).ValueGeneratedNever();
        builder.HasOne<Device>().WithOne().HasForeignKey<DeviceSyncState>(x => x.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Property(x => x.ConsecutiveFailureCount).HasDefaultValue(0);
        builder.Ignore(x => x.Id); // uses DeviceId as key
    }
}

/// <summary>Maps Devices.DeviceEventLog (insert-only) per 04 §6.4.</summary>
public sealed class DeviceEventLogConfiguration : IEntityTypeConfiguration<DeviceEventLog>
{
    public void Configure(EntityTypeBuilder<DeviceEventLog> builder)
    {
        builder.ToTable("DeviceEventLog", "Devices");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EventType).HasConversion<byte>();
        builder.Property(x => x.Outcome).HasConversion<byte>();
        builder.Property(x => x.Message).HasMaxLength(1000);
        builder.HasIndex(x => new { x.DeviceId, x.OccurredAtUtc }).HasDatabaseName("IX_DevLog_DeviceId_OccurredAt");
    }
}

/// <summary>Maps Workforce.EmployeeDeviceEnrollment with the BRULE-09 unique constraint (04 §6.2).</summary>
public sealed class EmployeeDeviceEnrollmentConfiguration : IEntityTypeConfiguration<EmployeeDeviceEnrollment>
{
    public void Configure(EntityTypeBuilder<EmployeeDeviceEnrollment> builder)
    {
        builder.ToTable("EmployeeDeviceEnrollment", "Workforce");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DeviceUserId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.Property(x => x.RowVersion).IsRowVersion();

        // (device, deviceUserId) → exactly one enrollment. (BRULE-09.)
        builder.HasIndex(x => new { x.DeviceId, x.DeviceUserId })
            .IsUnique().HasDatabaseName("UQ_Enroll_Device_DeviceUserId");

        builder.HasOne<Device>().WithMany().HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Cascade);
    }
}
