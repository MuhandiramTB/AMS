using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using TAMS.Application.Common.Ports;

namespace TAMS.Infrastructure.Devices;

/// <summary>
/// A working stand-in for the real ZKTeco SDK adapter (pending OQ-01). It models
/// a device's transaction buffer and can be driven into failure modes — outage,
/// duplicate emission — so the worker's resilience (watermark, idempotency,
/// offline recovery, reconciliation) can be exercised for real in dev and tests.
/// The concrete SDK adapter will implement the same IDeviceGateway port with no
/// changes to the worker or domain. (ADR-002/008, 10 §3 device double.)
/// </summary>
public sealed class SimulatedDeviceGateway : IDeviceGateway
{
    // Per-device (serial) transaction buffer, as the physical device would hold.
    private readonly ConcurrentDictionary<string, List<DeviceTransaction>> _buffers = new();
    // Per-device outage flag; when set, downloads throw (simulating unreachable).
    private readonly ConcurrentDictionary<string, bool> _outage = new();
    // Per-device "fail once mid-download" flag; models a network drop partway
    // through a transfer (throws after the buffer is read but before returning).
    private readonly ConcurrentDictionary<string, bool> _failNextDownload = new();

    private readonly ILogger<SimulatedDeviceGateway> _logger;

    public SimulatedDeviceGateway(ILogger<SimulatedDeviceGateway> logger) => _logger = logger;

    // --- Test/seed controls (used by the worker demo + resilience tests) ---

    /// <summary>Adds a transaction to a device's buffer (as if someone punched).</summary>
    public void EmitPunch(string serialNo, string deviceUserId, DateTime punchedAtUtc, int direction)
        => _buffers.GetOrAdd(serialNo, _ => new List<DeviceTransaction>())
            .Add(new DeviceTransaction(deviceUserId, punchedAtUtc, direction));

    /// <summary>Simulates the device/network going down (downloads will throw).</summary>
    public void SetOutage(string serialNo, bool down) => _outage[serialNo] = down;

    /// <summary>Arms a one-shot mid-download failure (network drop) for the next download.</summary>
    public void FailNextDownload(string serialNo) => _failNextDownload[serialNo] = true;

    public void ClearBuffer(string serialNo) => _buffers.TryRemove(serialNo, out _);

    // --- IDeviceGateway ---

    public Task<DeviceProbeResult> TestConnectionAsync(DeviceConnection connection, CancellationToken cancellationToken = default)
    {
        var down = _outage.TryGetValue(connection.SerialNo, out var v) && v;
        return Task.FromResult(new DeviceProbeResult(!down, down ? "Simulated outage." : "OK"));
    }

    public Task<IReadOnlyList<DeviceTransaction>> DownloadTransactionsAsync(
        DeviceConnection connection, DateTime? sinceUtc, CancellationToken cancellationToken = default)
    {
        if (_outage.TryGetValue(connection.SerialNo, out var down) && down)
        {
            // Model an outage as a thrown exception — the worker must NOT advance
            // the watermark on this path, so nothing is skipped once we recover.
            throw new IOException($"Device {connection.SerialNo} is unreachable (simulated).");
        }

        var buffer = _buffers.GetOrAdd(connection.SerialNo, _ => new List<DeviceTransaction>());

        // One-shot mid-download failure: read the buffer, then drop the connection
        // before returning — models a partial transfer. The worker must not advance
        // the watermark; the next cycle re-downloads and de-dupes.
        if (_failNextDownload.TryGetValue(connection.SerialNo, out var fail) && fail)
        {
            _failNextDownload[connection.SerialNo] = false;
            throw new IOException($"Network drop mid-download for {connection.SerialNo} (simulated).");
        }

        IReadOnlyList<DeviceTransaction> result = buffer
            .Where(t => sinceUtc is null || t.PunchedAtUtc > sinceUtc)
            .OrderBy(t => t.PunchedAtUtc)
            .ToList();

        _logger.LogInformation(
            "Simulated download {Serial} since {Since}: {Count} transactions",
            connection.SerialNo, sinceUtc, result.Count);
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<DeviceTransaction>> ListAllTransactionsAsync(
        DeviceConnection connection, CancellationToken cancellationToken = default)
    {
        var buffer = _buffers.GetOrAdd(connection.SerialNo, _ => new List<DeviceTransaction>());
        IReadOnlyList<DeviceTransaction> all = buffer.ToList();
        return Task.FromResult(all);
    }
}
