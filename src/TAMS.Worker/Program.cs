using Microsoft.EntityFrameworkCore;
using Serilog;
using TAMS.Application;
using TAMS.Application.Common.Ports;
using TAMS.Infrastructure;
using TAMS.Worker;

var builder = Host.CreateApplicationBuilder(args);

// Structured logging (NFR-25, 06 §11).
builder.Services.AddSerilog((_, config) => config
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

// Application + Infrastructure (shares the same domain/EF as the API — DRY, ADR-003).
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Worker-side context adapters (no HTTP request here). Scoped so each per-cycle
// scope gets a fresh correlation id.
builder.Services.AddScoped<ICorrelationIdAccessor, WorkerCorrelationIdAccessor>();
builder.Services.AddScoped<ICurrentUser, SystemUser>();

// Worker options + the hosted sync service.
var workerOptions = builder.Configuration.GetSection("Worker").Get<WorkerOptions>() ?? new WorkerOptions();
builder.Services.AddSingleton(workerOptions);
builder.Services.AddHostedService<DeviceSyncWorker>();

var host = builder.Build();

// Verify the schema is reachable before the sync loop starts. In Development we
// migrate (dev convenience); elsewhere we only assert connectivity + that the
// schema exists, failing fast with an actionable log rather than degrading into an
// infinite fail-log-wait loop against a missing/unreachable database. Production
// applies migrations via the controlled deploy step (11 §6), same as the API.
using (var scope = host.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var startupLogger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    var db = services.GetRequiredService<TAMS.Infrastructure.Persistence.TamsDbContext>();
    try
    {
        if (builder.Environment.IsDevelopment())
        {
            await db.Database.MigrateAsync();
        }
        else if (!await db.Database.CanConnectAsync())
        {
            throw new InvalidOperationException(
                "Worker cannot connect to the database. Ensure it is reachable and migrated before starting the worker.");
        }
    }
    catch (Exception ex)
    {
        startupLogger.LogCritical(ex, "Worker database readiness check failed at startup; aborting.");
        throw;
    }
}

host.Run();
