using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TAMS.Infrastructure.Persistence;

namespace TAMS.Api.Controllers;

/// <summary>Liveness/readiness endpoints. (05 §10.9, NFR-26.)</summary>
[ApiController]
[Route("api/v1/health")]
[AllowAnonymous]
[DisableRateLimiting] // monitoring probes must not be throttled
public sealed class HealthController : ControllerBase
{
    /// <summary>GET /api/v1/health/live — process is up.</summary>
    [HttpGet("live")]
    public IActionResult Live() => Ok(new { status = "live" });

    /// <summary>GET /api/v1/health/ready — dependencies (DB) reachable.</summary>
    [HttpGet("ready")]
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
