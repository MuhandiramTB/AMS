using TAMS.Domain.Common;

namespace TAMS.Domain.Devices;

/// <summary>
/// A registered ZKTeco terminal. Only registered devices are trusted — punches
/// from unknown serials are rejected (allow-list, 06 §12). (02 FR-ZK-010, 04 §6.4.)
/// </summary>
public sealed class Device : AuditableEntity
{
    private Device()
    {
    }

    public Device(string serialNo, string name, string? ipAddress = null, int? port = null, string? model = null)
    {
        SetSerialNo(serialNo);
        SetName(name);
        IpAddress = ipAddress;
        Port = port;
        Model = model;
        IsEnabled = true;
    }

    /// <summary>Unique device serial (UQ_Device_SerialNo) — the allow-list key.</summary>
    public string SerialNo { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;
    public string? IpAddress { get; private set; }
    public int? Port { get; private set; }
    public string? Model { get; private set; }
    public bool IsEnabled { get; private set; }

    /// <summary>Last time the device was successfully reached (for unreachable alerting, FR-ZK-011).</summary>
    public DateTime? LastSeenUtc { get; private set; }

    public void UpdateDetails(string name, string? ipAddress, int? port, string? model)
    {
        SetName(name);
        IpAddress = ipAddress;
        Port = port;
        Model = model;
    }

    public void Enable() => IsEnabled = true;

    public void Disable() => IsEnabled = false;

    public void MarkSeen(DateTime nowUtc) => LastSeenUtc = nowUtc;

    private void SetSerialNo(string serialNo)
    {
        if (string.IsNullOrWhiteSpace(serialNo))
        {
            throw new DomainException("Device serial number is required.");
        }

        SerialNo = serialNo.Trim();
    }

    private void SetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Device name is required.");
        }

        Name = name.Trim();
    }
}
