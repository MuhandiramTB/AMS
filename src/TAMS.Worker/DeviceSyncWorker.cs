using MediatR;
using TAMS.Application.Common.Ports;
using TAMS.Application.Devices;

namespace TAMS.Worker;

/// <summary>
/// The ZKTeco capture worker (ADR-002): a hosted background service that, on a
/// schedule, syncs every enabled device independently of user/API traffic. Each
/// cycle is a MediatR SyncDeviceCommand which does watermark-gated, idempotent,
/// reconciled ingestion (the resilience core lives in the handler, not here).
/// The worker only orchestrates scheduling, iteration and top-level error safety
/// so a single bad device never stops the loop. (FR-ZK-001/005/011.)
/// </summary>
public sealed class DeviceSyncWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DeviceSyncWorker> _logger;
    private readonly WorkerOptions _options;

    public DeviceSyncWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<DeviceSyncWorker> logger,
        WorkerOptions options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options;
    }

    // Consecutive cycle-level failures (e.g. DB unreachable) → exponential, capped
    // extra delay on top of the poll interval, so a sustained outage is retried with
    // backoff rather than a fixed-cadence storm. Reset on any successful cycle. (NFR-09.)
    private int _consecutiveCycleFailures;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Device sync worker started; interval {Interval}s", _options.PollIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunCycleAsync(stoppingToken);
                _consecutiveCycleFailures = 0;

                await Task.Delay(TimeSpan.FromSeconds(_options.PollIntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break; // graceful shutdown (including cancellation during the delay)
            }
            catch (Exception ex)
            {
                // Never let a cycle-level failure kill the loop; back off before retrying.
                _consecutiveCycleFailures++;
                var backoff = CycleBackoff();
                _logger.LogError(ex,
                    "Device sync cycle failed ({Count} consecutive); backing off {Backoff}s before retry.",
                    _consecutiveCycleFailures, backoff.TotalSeconds);

                try
                {
                    await Task.Delay(backoff, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Poll interval plus an exponentially-growing, capped penalty scaled by the
    /// number of consecutive cycle failures (interval·2^(n-1), capped at
    /// MaxBackoffCycles·interval).
    /// </summary>
    private TimeSpan CycleBackoff()
    {
        var interval = _options.PollIntervalSeconds;
        var multiplier = Math.Min(1 << Math.Min(_consecutiveCycleFailures - 1, 20), _options.MaxBackoffCycles);
        return TimeSpan.FromSeconds(interval * Math.Max(1, multiplier));
    }

    // Per-device backoff: after N consecutive failures, skip the device for a
    // number of cycles that grows exponentially (capped), so an unreachable device
    // isn't hammered every interval. (FR-ZK-005 backoff.)
    private readonly Dictionary<long, int> _skipCyclesRemaining = new();

    private async Task RunCycleAsync(CancellationToken stoppingToken)
    {
        // Enumerate devices in a short-lived scope.
        List<long> deviceIds;
        using (var listScope = _scopeFactory.CreateScope())
        {
            var repo = listScope.ServiceProvider.GetRequiredService<IDeviceRepository>();
            deviceIds = (await repo.GetEnabledAsync(stoppingToken)).Select(d => d.Id).ToList();
        }

        foreach (var deviceId in deviceIds)
        {
            stoppingToken.ThrowIfCancellationRequested();

            // Honour per-device backoff: skip this cycle if still cooling down.
            if (_skipCyclesRemaining.TryGetValue(deviceId, out var skip) && skip > 0)
            {
                _skipCyclesRemaining[deviceId] = skip - 1;
                continue;
            }

            // Fresh scope PER DEVICE → each device gets its own correlation id for
            // end-to-end traceability. (LOW fix; FR-ZK-009.)
            using var scope = _scopeFactory.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<ISender>();

            // Per-device timeout: a device that connects but never returns data must
            // not hang the whole cycle and starve the other devices. Cancel the stuck
            // sync and treat it as a failure (backoff), then move on. (NFR-08.)
            using var deviceCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            deviceCts.CancelAfter(TimeSpan.FromSeconds(_options.DeviceSyncTimeoutSeconds));

            try
            {
                var result = await mediator.Send(
                    new SyncDeviceCommand(deviceId, _options.UnreachableAlertThreshold), deviceCts.Token);

                if (result.Reachable)
                {
                    _skipCyclesRemaining.Remove(deviceId); // recovered → clear backoff
                    _logger.LogInformation(
                        "Synced device {DeviceId}: ingested {Ingested}, dup {Dup}, unresolved {Unresolved}, watermark advanced {Advanced}",
                        result.DeviceId, result.Ingested, result.Duplicates, result.Unresolved, result.WatermarkAdvanced);
                }
                else
                {
                    ApplyBackoff(deviceId);
                    _logger.LogWarning(
                        "Device {DeviceId} unreachable (alerted: {Alerted}); backing off {Skip} cycle(s). Watermark preserved.",
                        result.DeviceId, result.Alerted, _skipCyclesRemaining.GetValueOrDefault(deviceId));
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Genuine shutdown — not a device fault. Let the outer loop handle it.
                throw;
            }
            catch (OperationCanceledException)
            {
                // Per-device timeout fired (deviceCts, not stoppingToken): the device
                // hung. Treat as a failure and back off, but keep the loop moving.
                ApplyBackoff(deviceId);
                _logger.LogWarning(
                    "Device {DeviceId} sync timed out after {Timeout}s; backing off {Skip} cycle(s).",
                    deviceId, _options.DeviceSyncTimeoutSeconds, _skipCyclesRemaining.GetValueOrDefault(deviceId));
            }
            catch (Exception ex)
            {
                ApplyBackoff(deviceId);
                _logger.LogError(ex, "Sync failed for device {DeviceId}; backing off.", deviceId);
            }
        }
    }

    /// <summary>
    /// Exponentially increases the number of cycles a failing device is skipped
    /// (1, 2, 4, … capped at MaxBackoffCycles), so retries back off instead of
    /// hammering an unreachable device every interval. (FR-ZK-005.)
    /// </summary>
    private void ApplyBackoff(long deviceId)
    {
        var current = _skipCyclesRemaining.GetValueOrDefault(deviceId, 0);
        var next = current <= 0 ? 1 : Math.Min(current * 2, _options.MaxBackoffCycles);
        _skipCyclesRemaining[deviceId] = next;
    }
}

/// <summary>Worker scheduling options (bound from configuration, 12-Factor).</summary>
public sealed class WorkerOptions
{
    public int PollIntervalSeconds { get; set; } = 30;
    public int UnreachableAlertThreshold { get; set; } = 3;

    /// <summary>Upper bound on consecutive cycles a failing device is skipped.</summary>
    public int MaxBackoffCycles { get; set; } = 10;

    /// <summary>Per-device sync timeout; a hung device is cancelled after this. (NFR-08.)</summary>
    public int DeviceSyncTimeoutSeconds { get; set; } = 60;
}
