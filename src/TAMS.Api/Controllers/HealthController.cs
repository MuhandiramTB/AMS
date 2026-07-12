using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TAMS.Infrastructure.Persistence;

namespace TAMS.Api.Controllers;

/// <summary>Liveness/readiness endpoints. (05 §10.9, NFR-26.) Exposed at BOTH the
/// conventional root path (/health/*) — what the reverse proxy and external monitors
/// probe — and the versioned API path (/api/v1/health/*).</summary>
[ApiController]
[AllowAnonymous]
[DisableRateLimiting] // monitoring probes must not be throttled
public sealed class HealthController : ControllerBase
{
    /// <summary>GET /health/live (and /api/v1/health/live) — process is up.</summary>
    [HttpGet("/health/live")]
    [HttpGet("/api/v1/health/live")]
    public IActionResult Live() => Ok(new { status = "live" });

    /// <summary>GET /health/ready (and /api/v1/health/ready) — dependencies (DB) reachable.</summary>
    [HttpGet("/health/ready")]
    [HttpGet("/api/v1/health/ready")]
    public async Task<IActionResult> Ready(
        [FromServices] TamsDbContext db,
        CancellationToken cancellationToken)
    {
        var canConnect = await db.Database.CanConnectAsync(cancellationToken);
        return canConnect
            ? Ok(new { status = "ready", database = "ok" })
            : StatusCode(StatusCodes.Status503ServiceUnavailable, new { status = "not-ready", database = "unreachable" });
    }
}
