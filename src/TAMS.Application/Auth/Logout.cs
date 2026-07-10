using MediatR;
using TAMS.Application.Common.Ports;
using TAMS.Domain.Common;

namespace TAMS.Application.Auth;

/// <summary>
/// Revokes the current user's refresh tokens. Idempotent and safe to call even
/// with no active tokens. (FR-AUTH, 06 §6/§16.)
/// </summary>
public sealed record LogoutCommand(long UserId) : IRequest;

public sealed class LogoutHandler : IRequestHandler<LogoutCommand>
{
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public LogoutHandler(IUserRepository users, IUnitOfWork unitOfWork, IClock clock)
    {
        _users = users;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        await _users.RevokeAllRefreshTokensAsync(request.UserId, _clock.UtcNow, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
