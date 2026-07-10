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

    private async Task RunCycleAsync(CancellationToken stoppingToken)
    {
        // A fresh scope per cycle (scoped DbContext/handlers). (12-Factor, EF scoping.)
        using var scope = _scopeFactory.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<ISender>();
        var devices = scope.ServiceProvider.GetRequiredService<IDeviceRepository>();

        var enabled = await devices.GetEnabledAsync(stoppingToken);
        foreach (var device in enabled)
        {
            stoppingToken.ThrowIfCancellationRequested();
            try
            {
                var result = await mediator.Send(
                    new SyncDeviceCommand(device.Id, _options.UnreachableAlertThreshold), stoppingToken);

                if (result.Reachable)
                {
                    _logger.LogInformation(
                        "Synced device {DeviceId}: ingested {Ingested}, dup {Dup}, unresolved {Unresolved}, watermark advanced {Advanced}",
                        result.DeviceId, result.Ingested, result.Duplicates, result.Unresolved, result.WatermarkAdvanced);
                }
                else
                {
                    _logger.LogWarning(
                        "Device {DeviceId} unreachable this cycle (alerted: {Alerted}). Watermark preserved; will recover on reconnect.",
                        result.DeviceId, result.Alerted);
                }
            }
            catch (Exception ex)
            {
                // Isolate per-device failures so the rest of the fleet still syncs.
                _logger.LogError(ex, "Sync failed for device {DeviceId}", device.Id);
            }
        }
    }
}

/// <summary>Worker scheduling options (bound from configuration, 12-Factor).</summary>
public sealed class WorkerOptions
{
    public int PollIntervalSeconds { get; set; } = 30;
    public int UnreachableAlertThreshold { get; set; } = 3;
}
