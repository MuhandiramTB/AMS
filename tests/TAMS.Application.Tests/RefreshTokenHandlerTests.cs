using FluentAssertions;
using NSubstitute;
using TAMS.Application.Auth;
using TAMS.Application.Common.Exceptions;
using TAMS.Application.Common.Ports;
using TAMS.Domain.Common;
using TAMS.Domain.Identity;

namespace TAMS.Application.Tests;

public sealed class RefreshTokenHandlerTests
{
    private static readonly DateTime Now = new(2026, 7, 10, 8, 0, 0, DateTimeKind.Utc);

    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly ITokenService _tokens = Substitute.For<ITokenService>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IClock _clock = Substitute.For<IClock>();

    public RefreshTokenHandlerTests()
    {
        _clock.UtcNow.Returns(Now);
        _tokens.HashRefreshToken(Arg.Any<string>()).Returns(ci => "hash:" + ci.Arg<string>());
    }

    private RefreshTokenHandler CreateHandler() => new(_users, _tokens, _uow, _clock);

    private static User ActiveUser()
    {
        var user = new User("nadia", "nadia@corp.com", "pw-hash");
        return user;
    }

    [Fact]
    public async Task UnknownToken_ThrowsAuthentication()
    {
        _users.GetRefreshTokenByHashAsync("hash:missing", Arg.Any<CancellationToken>())
            .Returns((RefreshToken?)null);

        var act = () => CreateHandler().Handle(new RefreshTokenCommand("missing"), default);

        await act.Should().ThrowAsync<AuthenticationException>();
    }

    [Fact]
    public async Task ValidToken_RotatesAndIssuesNewPair()
    {
        var user = ActiveUser();
        var stored = new RefreshToken(user.Id, "hash:good", Now.AddDays(1));
        _users.GetRefreshTokenByHashAsync("hash:good", Arg.Any<CancellationToken>()).Returns(stored);
        _users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        _tokens.IssueTokens(user).Returns(new TokenPair("new-access", 900, "new-refresh", "new-hash", Now.AddDays(7)));

        var result = await CreateHandler().Handle(new RefreshTokenCommand("good"), default);

        result.AccessToken.Should().Be("new-access");
        stored.RevokedAtUtc.Should().Be(Now); // old token rotated out
        await _users.Received().AddRefreshTokenAsync(
            Arg.Is<RefreshToken>(t => t.TokenHash == "new-hash"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReusedRevokedToken_RevokesAllAndThrows()
    {
        var user = ActiveUser();
        var revoked = new RefreshToken(user.Id, "hash:reused", Now.AddDays(1));
        revoked.Revoke(Now.AddMinutes(-5)); // already revoked → reuse
        _users.GetRefreshTokenByHashAsync("hash:reused", Arg.Any<CancellationToken>()).Returns(revoked);
        _users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var act = () => CreateHandler().Handle(new RefreshTokenCommand("reused"), default);

        await act.Should().ThrowAsync<AuthenticationException>();
        await _users.Received().RevokeAllRefreshTokensAsync(user.Id, Now, Arg.Any<CancellationToken>());
    }
}
