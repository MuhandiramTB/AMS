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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Device sync worker started; interval {Interval}s", _options.PollIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunCycleAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break; // graceful shutdown
            }
            catch (Exception ex)
            {
                // Never let a cycle-level failure kill the loop.
                _logger.LogError(ex, "Device sync cycle failed; will retry next interval.");
            }

            await Task.Delay(TimeSpan.FromSeconds(_options.PollIntervalSeconds), stoppingToken);
        }
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

            try
            {
                var result = await mediator.Send(
                    new SyncDeviceCommand(deviceId, _options.UnreachableAlertThreshold), stoppingToken);

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
}
