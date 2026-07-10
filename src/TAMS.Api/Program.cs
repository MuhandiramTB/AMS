using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using TAMS.Api.Auth;
using TAMS.Api.Common;
using TAMS.Application;
using TAMS.Application.Common.Ports;
using TAMS.Infrastructure;
using TAMS.Infrastructure.Persistence;
using TAMS.Infrastructure.Security;

var builder = WebApplication.CreateBuilder(args);

// --- Serilog structured logging (NFR-25, 06 §11) ---
builder.Host.UseSerilog((context, config) => config
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

// --- Application + Infrastructure layers (composition root) ---
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// --- HTTP context / current-user / correlation id (03 §9) ---
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CorrelationIdAccessor>();
builder.Services.AddScoped<ICorrelationIdAccessor>(sp => sp.GetRequiredService<CorrelationIdAccessor>());
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

// --- AuthN: JWT bearer (06 §6) ---
// Bearer validation parameters are configured from the bound JwtOptions via
// IOptions (not a value captured at startup), so they reflect the final,
// fully-layered configuration — issuance (JwtTokenService) and validation always
// share the same signing key, including under test host config overrides.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

builder.Services
    .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
    {
        var jwt = jwtOptions.Value;
        bearer.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

// --- AuthZ: permission-based, deny-by-default (06 §5) ---
builder.Services.AddAuthorization();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

// --- MVC + error handling (RFC 9457, 05 §6) ---
builder.Services.AddControllers();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// --- CORS: allow-list the SPA origin only (06 §13) ---
const string CorsPolicy = "SpaCors";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? Array.Empty<string>();
builder.Services.AddCors(options => options.AddPolicy(CorsPolicy, policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

// --- Rate limiting: strict per-IP on auth (brute-force defence), a generous
//     global limiter elsewhere; both return 429. (06 §13, FR-AUTH-005.)
//     Limits are configurable so non-prod (e.g. shared-IP test hosts) can relax
//     them without weakening the production defaults. ---
const string AuthRateLimit = "auth";
var authPermitLimit = builder.Configuration.GetValue<int?>("RateLimit:AuthPerMinute") ?? 10;
var globalPermitLimit = builder.Configuration.GetValue<int?>("RateLimit:GlobalPerMinute") ?? 300;
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy(AuthRateLimit, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = authPermitLimit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = globalPermitLimit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

var app = builder.Build();

// --- Migrate + seed at startup (dev convenience; prod migrates via controlled
//     deploy step per 04 §14 / 11 §6). ---
await using (var scope = app.Services.CreateAsyncScope())
{
    var services = scope.ServiceProvider;
    var db = services.GetRequiredService<TamsDbContext>();
    await db.Database.MigrateAsync();

    if (builder.Configuration.GetValue<bool>("Seed:Enabled"))
    {
        var seeder = services.GetRequiredService<DatabaseSeeder>();
        var admin = builder.Configuration.GetSection("BootstrapAdmin");
        await seeder.SeedAsync(
            admin.GetValue<string>("UserName") ?? "admin",
            admin.GetValue<string>("Email") ?? "admin@tams.local",
            admin.GetValue<string>("Password") ?? "ChangeMe!123");
    }
}

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts();
}

app.UseSerilogRequestLogging();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();

// HTTPS redirection is redundant in dev/test (no TLS listener) and would turn
// plain-HTTP test requests into empty-bodied 307s. In production TLS terminates
// at the reverse proxy (11 §7); enforce redirect only outside Development.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors(CorsPolicy);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

/// <summary>Exposed so integration tests can host the API via WebApplicationFactory.</summary>
public partial class Program;
