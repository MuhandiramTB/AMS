using System.Security.Claims;
using TAMS.Application.Common.Ports;
using TAMS.Infrastructure.Security;

namespace TAMS.Api.Common;

/// <summary>
/// Resolves the authenticated principal from the JWT on the current HTTP request.
/// Used for audit attribution and authorization. (06 §5, FR-AUD-001.)
/// </summary>
public sealed class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public long? UserId
    {
        get
        {
            var sub = Principal?.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? Principal?.FindFirstValue("sub");
            return long.TryParse(sub, out var id) ? id : null;
        }
    }

    public string UserName =>
        Principal?.FindFirstValue(ClaimTypes.Name)
        ?? Principal?.FindFirstValue("unique_name")
        ?? "anonymous";

    public IReadOnlyCollection<string> Permissions =>
        Principal?.FindAll(JwtTokenService.PermissionClaimType).Select(c => c.Value).ToList()
        ?? new List<string>();

    public long? EmployeeId
    {
        get
        {
            var value = Principal?.FindFirstValue(JwtTokenService.EmployeeIdClaimType);
            return long.TryParse(value, out var id) ? id : null;
        }
    }

    public bool HasPermission(string permission) =>
        Principal?.HasClaim(JwtTokenService.PermissionClaimType, permission) ?? false;
}
