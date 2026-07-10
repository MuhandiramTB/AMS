using System.Reflection;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using TAMS.Application.Common.Behaviours;

namespace TAMS.Application;

/// <summary>
/// Registers the Application layer: MediatR handlers, validators and the
/// cross-cutting pipeline behaviours (validation → logging), applied to every
/// request. (03 §9, ADR-007.)
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            // Order per ADR-007 (03 §9): validation → logging → handler. MediatR
            // runs behaviours in registration order (first = outermost), so
            // validation is registered first and rejects bad input before the
            // request enters the logged/handler pipeline.
            cfg.AddOpenBehavior(typeof(ValidationBehaviour<,>));
            cfg.AddOpenBehavior(typeof(LoggingBehaviour<,>));
        });

        services.AddValidatorsFromAssembly(assembly);

        // Pure domain services (no state/I/O) — safe as singletons. (ADR-006.)
        services.AddSingleton<Domain.Attendance.AttendanceCalculator>();
        services.AddSingleton<Domain.Scheduling.ShiftResolver>();

        return services;
    }
}
