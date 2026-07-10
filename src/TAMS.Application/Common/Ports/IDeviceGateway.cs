namespace TAMS.Application.Common.Ports;

/// <summary>A raw transaction as read from a device (before resolution to an employee).</summary>
public sealed record DeviceTransaction(
    string DeviceUserId,
    DateTime PunchedAtUtc,
    int Direction); // 0=Unknown, 1=In, 2=Out — mirrors PunchDirection

/// <summary>Result of a device connectivity probe.</summary>
public sealed record DeviceProbeResult(bool Reachable, string? Message);

/// <summary>
/// Port abstracting a physical attendance device. The concrete ZKTeco SDK adapter
/// lives in Infrastructure and can be swapped without touching the worker/domain
/// once the device model is confirmed (OQ-01, ADR-002/008). A simulator adapter
/// implements this for development and the resilience test suite.
/// </summary>
public interface IDeviceGateway
{
    /// <summary>Checks whether the device is currently reachable.</summary>
    Task<DeviceProbeResult> TestConnectionAsync(
        DeviceConnection connection, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads transactions punched strictly after <paramref name="sinceUtc"/>
    /// (null = from the beginning). Implementations may throw to signal an outage;
    /// the worker treats that as a failed cycle and does NOT advance the watermark.
    /// </summary>
    Task<IReadOnlyList<DeviceTransaction>> DownloadTransactionsAsync(
        DeviceConnection connection, DateTime? sinceUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the device's full set of transaction identities for reconciliation
    /// (comparing what the device holds vs what we ingested). Used to prove no gaps.
    /// </summary>
    Task<IReadOnlyList<DeviceTransaction>> ListAllTransactionsAsync(
        DeviceConnection connection, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pushes the set of enrolled device-user-ids to the terminal so it recognises
    /// them (outbound half of enrollment sync, FR-ZK-003). The real SDK adapter
    /// provisions users on the device; the simulator records the intent. Returns
    /// the count accepted by the device.
    /// </summary>
    Task<int> SyncEnrollmentsToDeviceAsync(
        DeviceConnection connection, IReadOnlyList<string> deviceUserIds, CancellationToken cancellationToken = default);
}

/// <summary>Connection details passed to the gateway (kept out of the domain).</summary>
public sealed record DeviceConnection(long DeviceId, string SerialNo, string? IpAddress, int? Port);
