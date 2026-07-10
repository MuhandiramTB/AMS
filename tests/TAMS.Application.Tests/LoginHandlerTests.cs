using FluentAssertions;
using NSubstitute;
using TAMS.Application.Auth;
using TAMS.Application.Common.Exceptions;
using TAMS.Application.Common.Ports;
using TAMS.Domain.Common;
using TAMS.Domain.Identity;

namespace TAMS.Application.Tests;

public sealed class LoginHandlerTests
{
    private static readonly DateTime Now = new(2026, 7, 10, 8, 0, 0, DateTimeKind.Utc);

    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly ITokenService _tokens = Substitute.For<ITokenService>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly IAuthPolicyOptions _authOptions = Substitute.For<IAuthPolicyOptions>();

    public LoginHandlerTests()
    {
        _clock.UtcNow.Returns(Now);
        _authOptions.LockoutDuration.Returns(TimeSpan.FromMinutes(15));
    }

    private LoginHandler CreateHandler() =>
        new(_users, _hasher, _tokens, _uow, _clock, _authOptions);

    [Fact]
    public async Task UnknownUser_ThrowsGenericAuthenticationException()
    {
        _users.GetByUserNameAsync("ghost", Arg.Any<CancellationToken>()).Returns((User?)null);

        var act = () => CreateHandler().Handle(new LoginCommand("ghost", "pw"), default);

        // Generic — no user enumeration. (06 §4.)
        await act.Should().ThrowAsync<AuthenticationException>();
    }

    [Fact]
    public async Task WrongPassword_RegistersFailureAndThrows()
    {
        var user = new User("nadia", "nadia@corp.com", "hash");
        _users.GetByUserNameAsync("nadia", Arg.Any<CancellationToken>()).Returns(user);
        _hasher.Verify("bad", "hash").Returns(false);

        var act = () => CreateHandler().Handle(new LoginCommand("nadia", "bad"), default);

        await act.Should().ThrowAsync<AuthenticationException>();
        user.FailedLoginCount.Should().Be(1);
        await _uow.Received().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ValidCredentials_IssuesTokensAndPersistsRefreshToken()
    {
        var user = new User("nadia", "nadia@corp.com", "hash");
        var role = new Role("HROfficer");
        role.GrantPermission(new Permission(Permissions.EmployeeRead));
        user.AssignRole(role);

        _users.GetByUserNameAsync("nadia", Arg.Any<CancellationToken>()).Returns(user);
        _hasher.Verify("good", "hash").Returns(true);
        _tokens.IssueTokens(user).Returns(new TokenPair(
            "access", 900, "refresh", "refresh-hash", Now.AddDays(7)));

        var result = await CreateHandler().Handle(new LoginCommand("nadia", "good"), default);

        result.AccessToken.Should().Be("access");
        result.User.Permissions.Should().Contain(Permissions.EmployeeRead);
        await _users.Received().AddRefreshTokenAsync(
            Arg.Is<RefreshToken>(t => t.TokenHash == "refresh-hash"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LockedOutUser_ThrowsAccountLocked()
    {
        var user = new User("nadia", "nadia@corp.com", "hash");
        for (var i = 0; i < User.MaxFailedAttempts; i++)
        {
            user.RegisterFailedLogin(Now, TimeSpan.FromMinutes(15));
        }

        _users.GetByUserNameAsync("nadia", Arg.Any<CancellationToken>()).Returns(user);

        var act = () => CreateHandler().Handle(new LoginCommand("nadia", "whatever"), default);

        await act.Should().ThrowAsync<AccountLockedException>();
    }
}
