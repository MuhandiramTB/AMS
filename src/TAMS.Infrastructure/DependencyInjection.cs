using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TAMS.Application.Common.Ports;
using TAMS.Domain.Common;
using TAMS.Infrastructure.Common;
using TAMS.Infrastructure.Persistence;
using TAMS.Infrastructure.Persistence.Repositories;
using TAMS.Infrastructure.Security;

namespace TAMS.Infrastructure;

/// <summary>
/// Registers Infrastructure: EF Core (SQL Server), repositories, unit of work,
/// audit interceptor, security services, and the seeder. This is the only place
/// that references concrete infrastructure types. (03 §7, 07 §4.)
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Options
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddSingleton<IAuthPolicyOptions>(sp =>
            sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<JwtOptions>>().Value);

        // Cross-cutting
        services.AddSingleton<IClock, SystemClock>();

        // Audit trail builder (scoped: depends on current user + correlation id).
        // The DbContext uses it to persist business data and audit rows atomically.
        services.AddScoped<AuditTrailBuilder>();

        // EF Core — SQL Server (04, 11). Connection string from configuration.
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");

        services.AddDbContext<TamsDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<TamsDbContext>());

        // Repositories
        services.AddScoped<IEmployeeRepository, EmployeeRepository>();
        services.AddScoped<IDepartmentRepository, DepartmentRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ISchedulingRepository, SchedulingRepository>();
        services.AddScoped<IAttendanceRepository, AttendanceRepository>();

        // Security
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddScoped<ITokenService, JwtTokenService>();

        // Seeder
        services.AddScoped<DatabaseSeeder>();

        return services;
    }
}
