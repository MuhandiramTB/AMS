using TAMS.Domain.Common;

namespace TAMS.Domain.Devices;

/// <summary>
/// Per-device sync pointer — the crash-safe resume watermark that makes capture
/// exactly-once and loss-free (ADR-011, KPI-04, 04 §6.4). The watermark is only
/// advanced AFTER punches up to that point are durably ingested, so a failure
/// never skips un-ingested data. 1:1 with <see cref="Device"/>.
/// </summary>
public sealed class DeviceSyncState : Entity
{
    private DeviceSyncState()
    {
    }

    public DeviceSyncState(long deviceId)
    {
        DeviceId = deviceId;
    }

    public long DeviceId { get; private set; }

    /// <summary>Timestamp of the last successfully-ingested transaction. Download
    /// resumes strictly after this. Null = never synced (download from the start).</summary>
    public DateTime? LastWatermarkUtc { get; private set; }

    public DateTime? LastSyncStartedUtc { get; private set; }
    public DateTime? LastSyncSucceededUtc { get; private set; }

    /// <summary>Consecutive failed sync attempts — drives the unreachable alert (FR-ZK-011).</summary>
    public int ConsecutiveFailureCount { get; private set; }

    public void BeginSync(DateTime nowUtc) => LastSyncStartedUtc = nowUtc;

    /// <summary>
    /// Records a successful cycle and advances the watermark. Only call after the
    /// downloaded punches are committed. The watermark never moves backwards.
    /// </summary>
    public void CompleteSync(DateTime nowUtc, DateTime? newWatermarkUtc)
    {
        LastSyncSucceededUtc = nowUtc;
        ConsecutiveFailureCount = 0;

        if (newWatermarkUtc is not null &&
            (LastWatermarkUtc is null || newWatermarkUtc > LastWatermarkUtc))
        {
            LastWatermarkUtc = newWatermarkUtc;
        }
    }

    public void RecordFailure() => ConsecutiveFailureCount++;

    /// <summary>True when failures have crossed the alert threshold (FR-ZK-011).</summary>
    public bool IsUnreachable(int threshold) => ConsecutiveFailureCount >= threshold;
}
