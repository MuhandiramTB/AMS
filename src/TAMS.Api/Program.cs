using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("Jwt configuration section is missing.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
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
app.UseHttpsRedirection();
app.UseCors(CorsPolicy);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

/// <summary>Exposed so integration tests can host the API via WebApplicationFactory.</summary>
public partial class Program;
