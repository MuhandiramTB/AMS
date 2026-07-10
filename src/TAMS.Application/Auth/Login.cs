using FluentValidation;
using MediatR;
using TAMS.Application.Common.Exceptions;
using TAMS.Application.Common.Ports;
using TAMS.Domain.Common;
using TAMS.Domain.Identity;

namespace TAMS.Application.Auth;

/// <summary>Authenticates a user and issues tokens. (FR-AUTH-001/002/005/006.)</summary>
public sealed record LoginCommand(string UserName, string Password) : IRequest<LoginResultDto>;

public sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(x => x.UserName).NotEmpty();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public sealed class LoginHandler : IRequestHandler<LoginCommand, LoginResultDto>
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly IAuthPolicyOptions _authOptions;

    public LoginHandler(
        IUserRepository users,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IUnitOfWork unitOfWork,
        IClock clock,
        IAuthPolicyOptions authOptions)
    {
        _users = users;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _authOptions = authOptions;
    }

    public async Task<LoginResultDto> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var user = await _users.GetByUserNameAsync(request.UserName, cancellationToken);

        // Uniform handling: unknown user and wrong password both yield a generic
        // 401, preventing user enumeration. (06 §4.)
        if (user is null || !user.IsActive)
        {
            // Perform a dummy verify so the unknown/inactive-user path costs
            // roughly the same as a real password check, mitigating the timing
            // side-channel the security doc calls out (06 §4.2, "constant-time").
            _passwordHasher.VerifyDummy(request.Password);
            throw new AuthenticationException();
        }

        if (user.IsLockedOut(now))
        {
            throw new AccountLockedException();
        }

        if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            user.RegisterFailedLogin(now, _authOptions.LockoutDuration);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            throw new AuthenticationException();
        }

        user.RegisterSuccessfulLogin(now);

        var tokens = _tokenService.IssueTokens(user);
        await _users.AddRefreshTokenAsync(
            new RefreshToken(user.Id, tokens.RefreshTokenHash, tokens.RefreshExpiresAtUtc),
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
