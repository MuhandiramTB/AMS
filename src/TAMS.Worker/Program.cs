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
host.Run();
