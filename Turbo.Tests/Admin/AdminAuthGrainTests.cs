using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Turbo.Admin.Configuration;
using Turbo.Primitives.Admin.Enums;
using Turbo.Primitives.Admin.Grains;
using Turbo.Primitives.Players;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Admin;

/// <summary>
/// The admin panel's short-lived steps: a setup link works once and only for its hours, a passkey
/// ceremony is taken once and only as what it was started for, a session ends when it should, and
/// nobody piles up sessions without bound.
/// </summary>
public sealed class AdminAuthGrainTests
{
    private static readonly PlayerId ALICE = new(7);
    private static readonly PlayerId BOB = new(8);

    private readonly ManualTimeProvider _time = new(
        new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero)
    );

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ASetupLinkNamesItsPlayerUntilItIsSpent()
    {
        var grain = Grain();
        var link = await grain.CreateSetupTokenAsync(ALICE, Ct);

        link.ExpiresAtUtc.Should().Be(_time.GetUtcNow().UtcDateTime.AddHours(24));
        (await grain.GetSetupPlayerAsync(link.Token, Ct)).Should().Be(ALICE);
        (await grain.GetSetupPlayerAsync(link.Token, Ct))
            .Should()
            .Be(ALICE, "looking does not spend it");
        (await grain.RedeemSetupTokenAsync(link.Token, Ct)).Should().Be(ALICE);
        (await grain.RedeemSetupTokenAsync(link.Token, Ct)).Should().BeNull();
        (await grain.GetSetupPlayerAsync(link.Token, Ct)).Should().BeNull();
    }

    [Fact]
    public async Task ASetupLinkStopsWorkingAfterItsHours()
    {
        var grain = Grain();
        var link = await grain.CreateSetupTokenAsync(ALICE, Ct);

        _time.Advance(TimeSpan.FromHours(24));

        (await grain.GetSetupPlayerAsync(link.Token, Ct)).Should().BeNull();
        (await grain.RedeemSetupTokenAsync(link.Token, Ct)).Should().BeNull();
    }

    [Fact]
    public async Task ACeremonyIsTakenOnceAndOnlyAsWhatItWasStartedFor()
    {
        var grain = Grain();
        var register = await grain.BeginCeremonyAsync(ALICE, AdminCeremonyKind.Register, "{}", Ct);
        var signIn = await grain.BeginCeremonyAsync(
            ALICE,
            AdminCeremonyKind.SignIn,
            "{\"a\":1}",
            Ct
        );

        (await grain.TakeCeremonyAsync(register, AdminCeremonyKind.SignIn, Ct))
            .Should()
            .BeNull("a registration cannot finish a sign-in");
        (await grain.TakeCeremonyAsync(register, AdminCeremonyKind.Register, Ct))
            .Should()
            .BeNull("a ceremony presented for the wrong step is spent");

        var taken = await grain.TakeCeremonyAsync(signIn, AdminCeremonyKind.SignIn, Ct);
        taken!.PlayerId.Should().Be(ALICE);
        taken.OptionsJson.Should().Be("{\"a\":1}");
        (await grain.TakeCeremonyAsync(signIn, AdminCeremonyKind.SignIn, Ct)).Should().BeNull();
    }

    [Fact]
    public async Task ACeremonyLapsesAfterItsMinutes()
    {
        var grain = Grain();
        var ceremony = await grain.BeginCeremonyAsync(ALICE, AdminCeremonyKind.SignIn, "{}", Ct);

        _time.Advance(TimeSpan.FromMinutes(5));

        (await grain.TakeCeremonyAsync(ceremony, AdminCeremonyKind.SignIn, Ct)).Should().BeNull();
    }

    [Fact]
    public async Task ASessionLastsItsHours()
    {
        var grain = Grain();
        var grant = await grain.OpenSessionAsync(ALICE, Ct);

        grant.Session.ExpiresAtUtc.Should().Be(_time.GetUtcNow().UtcDateTime.AddHours(12));
        (await grain.GetSessionAsync(grant.SessionToken, Ct))!.PlayerId.Should().Be(ALICE);

        _time.Advance(TimeSpan.FromHours(12));

        (await grain.GetSessionAsync(grant.SessionToken, Ct)).Should().BeNull();
    }

    [Fact]
    public async Task SigningOutEndsTheSession()
    {
        var grain = Grain();
        var grant = await grain.OpenSessionAsync(ALICE, Ct);

        await grain.EndSessionAsync(grant.SessionToken, Ct);

        (await grain.GetSessionAsync(grant.SessionToken, Ct)).Should().BeNull();
    }

    [Fact]
    public async Task SigningInPastTheLimitEndsThePlayersOldestSessionOnly()
    {
        var grain = Grain(new AdminConfig { MaxSessionsPerPlayer = 2 });
        var bobs = (await grain.OpenSessionAsync(BOB, Ct)).SessionToken;
        var first = (await grain.OpenSessionAsync(ALICE, Ct)).SessionToken;
        _time.Advance(TimeSpan.FromMinutes(1));
        var second = (await grain.OpenSessionAsync(ALICE, Ct)).SessionToken;
        _time.Advance(TimeSpan.FromMinutes(1));
        var third = (await grain.OpenSessionAsync(ALICE, Ct)).SessionToken;

        (await grain.GetSessionAsync(first, Ct)).Should().BeNull();
        (await grain.GetSessionAsync(second, Ct)).Should().NotBeNull();
        (await grain.GetSessionAsync(third, Ct)).Should().NotBeNull();
        (await grain.GetSessionAsync(bobs, Ct))
            .Should()
            .NotBeNull("another player's sessions are theirs");
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-token")]
    public async Task AnUnknownTokenOpensNothing(string token)
    {
        var grain = Grain();

        (await grain.GetSetupPlayerAsync(token, Ct)).Should().BeNull();
        (await grain.RedeemSetupTokenAsync(token, Ct)).Should().BeNull();
        (await grain.GetSessionAsync(token, Ct)).Should().BeNull();
        (await grain.TakeCeremonyAsync(token, AdminCeremonyKind.SignIn, Ct)).Should().BeNull();
    }

    [Fact]
    public async Task ASetupLinkIsNotASession()
    {
        var grain = Grain();
        var link = await grain.CreateSetupTokenAsync(ALICE, Ct);

        (await grain.GetSessionAsync(link.Token, Ct)).Should().BeNull();
    }

    private IAdminAuthGrain Grain(AdminConfig? config = null)
    {
        var type = typeof(AdminConfig).Assembly.GetType("Turbo.Admin.Grains.AdminAuthGrain")!;

        return (IAdminAuthGrain)
            Activator.CreateInstance(
                type,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                [Options.Create(config ?? new AdminConfig()), _time],
                culture: null
            )!;
    }
}
