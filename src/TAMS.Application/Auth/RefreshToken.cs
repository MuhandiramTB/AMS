using FluentValidation;
using MediatR;
using TAMS.Application.Common.Exceptions;
using TAMS.Application.Common.Ports;
using TAMS.Domain.Common;
using DomainRefreshToken = TAMS.Domain.Identity.RefreshToken;

namespace TAMS.Application.Auth;

/// <summary>
/// Exchanges a valid refresh token for a new access+refresh pair, rotating the
/// refresh token (one-time use). If a revoked/expired token is presented, all of
/// the user's tokens are revoked as a reuse-detection response. (FR-AUTH-002, 06 §6.)
/// </summary>
public sealed record RefreshTokenCommand(string RefreshToken) : IRequest<LoginResultDto>;

public sealed class RefreshTokenValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}

public sealed class RefreshTokenHandler : IRequestHandler<RefreshTokenCommand, LoginResultDto>
{
    private readonly IUserRepository _users;
    private readonly ITokenService _tokenService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public RefreshTokenHandler(
        IUserRepository users,
        ITokenService tokenService,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _users = users;
        _tokenService = tokenService;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<LoginResultDto> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var hash = _tokenService.HashRefreshToken(request.RefreshToken);
        var stored = await _users.GetRefreshTokenByHashAsync(hash, cancellationToken);

        if (stored is null)
        {
            throw new AuthenticationException();
        }

        var user = await _users.GetByIdAsync(stored.UserId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            throw new AuthenticationException();
        }

        // Reuse detection: a presented-but-inactive token means it was already
        // rotated or revoked → treat as compromise, revoke the whole family.
        if (!stored.IsActive(now))
        {
            await _users.RevokeAllRefreshTokensAsync(user.Id, now, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            throw new AuthenticationException();
        }

        // Rotate: revoke the used token, issue a new pair.
        stored.Revoke(now);
        var tokens = _tokenService.IssueTokens(user);
        await _users.AddRefreshTokenAsync(
            new DomainRefreshToken(user.Id, tokens.RefreshTokenHash, tokens.RefreshExpiresAtUtc),
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var userDto = new AuthUserDto(
            user.Id,
            user.UserName,
            user.Roles.Select(r => r.Name).ToList(),
            user.PermissionCodes);

        return new LoginResultDto(
            tokens.AccessToken,
            "Bearer",
            tokens.ExpiresInSeconds,
            tokens.RefreshToken,
            userDto);
    }
}
