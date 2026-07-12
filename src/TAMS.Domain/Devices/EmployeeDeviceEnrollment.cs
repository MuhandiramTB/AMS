using TAMS.Domain.Common;

namespace TAMS.Domain.Devices;

/// <summary>
/// Maps an employee to their identity on a specific device. A (device, deviceUserId)
/// pair resolves to exactly one employee, guaranteeing a punch is never attributed
/// to the wrong person (BRULE-09, enforced by a unique constraint). An employee may
/// enrol on multiple devices. (02 FR-EMP-004, FR-ZK-003, 04 §6.2.)
/// </summary>
public sealed class EmployeeDeviceEnrollment : AuditableEntity
{
    private EmployeeDeviceEnrollment()
    {
    }

    public EmployeeDeviceEnrollment(long employeeId, long deviceId, string deviceUserId)
    {
        if (string.IsNullOrWhiteSpace(deviceUserId))
        {
            throw new DomainException("Device user id is required for enrollment.");
        }

        EmployeeId = employeeId;
        DeviceId = deviceId;
        DeviceUserId = deviceUserId.Trim();
        IsActive = true;
    }

    public long EmployeeId { get; private set; }
    public long DeviceId { get; private set; }

    /// <summary>The employee's identifier on the device (as reported in punches).</summary>
    public string DeviceUserId { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public void Deactivate() => IsActive = false;

    /// <summary>Re-uses a freed device slot: point it at a new employee and reactivate.
    /// Lets a slot vacated by a leaver be enrolled to a new hire without a duplicate
    /// row (the (device, deviceUserId) pair is unique). (FR-ZK-003.)</summary>
    public void ReassignTo(long employeeId)
    {
        EmployeeId = employeeId;
        IsActive = true;
    }
}
