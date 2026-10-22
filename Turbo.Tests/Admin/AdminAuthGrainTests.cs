using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Admin.Configuration;
using Turbo.Database.Entities.Admin;
using Turbo.Database.Entities.Players;
using Turbo.Primitives.Admin.Enums;
using Turbo.Primitives.Admin.Grains;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Admin;

/// <summary>
/// The admin panel's short-lived steps: a setup link works once and only for its hours, a passkey
/// ceremony is taken once and only as what it was started for, a session ends when it should, and
/// nobody piles up sessions without bound. A session outlives a restart (a fresh activation over
/// the same database) and one that ended stays ended.
/// </summary>
public sealed class AdminAuthGrainTests : IDisposable
{
    private static readonly PlayerId ALICE = new(7);
    private static readonly PlayerId BOB = new(8);

    private readonly ManualTimeProvider _time = new(
        new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero)
    );

    private readonly SqliteDb _db = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public AdminAuthGrainTests()
    {
        foreach (var (id, name) in new[] { (ALICE, "alice"), (BOB, "bob") })
            _db.Insert(
                new PlayerEntity
                {
                    Id = id.Value,
                    Name = name,
                    Figure = "hd-180-1",
                    Gender = AvatarGenderType.Male,
                    PlayerStatus = PlayerStatusType.Offline,
                }
            );
    }

    public void Dispose() => _db.Dispose();

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

    [Fact]
    public async Task ASessionOutlivesARestart()
    {
        var grain = Grain();
        var grant = await grain.OpenSessionAsync(ALICE, Ct);

        var restarted = await RestartAsync();
        var session = await restarted.GetSessionAsync(grant.SessionToken, Ct);

        session!.PlayerId.Should().Be(ALICE);
        session
            .ExpiresAtUtc.Should()
            .Be(grant.Session.ExpiresAtUtc, "the same 12 hours, not new ones");
        session.ExpiresAtUtc.Kind.Should().Be(DateTimeKind.Utc, "the panel reads it as UTC");
        session.Id.Should().Be(grant.Session.Id);

        _time.Advance(TimeSpan.FromHours(12));

        (await restarted.GetSessionAsync(grant.SessionToken, Ct))
            .Should()
            .BeNull("a restart does not stretch a session");
    }

    [Fact]
    public async Task ASignedOutSessionStaysOutAfterARestart()
    {
        var grain = Grain();
        var grant = await grain.OpenSessionAsync(ALICE, Ct);

        await grain.EndSessionAsync(grant.SessionToken, Ct);

        (await (await RestartAsync()).GetSessionAsync(grant.SessionToken, Ct)).Should().BeNull();
        (await Rows()).Should().BeEmpty();
    }

    [Fact]
    public async Task ASessionEndedByTheLimitStaysEndedAfterARestart()
    {
        var grain = Grain(new AdminConfig { MaxSessionsPerPlayer = 1 });
        var first = (await grain.OpenSessionAsync(ALICE, Ct)).SessionToken;
        var second = (await grain.OpenSessionAsync(ALICE, Ct)).SessionToken;

        var restarted = await RestartAsync();

        (await restarted.GetSessionAsync(first, Ct)).Should().BeNull();
        (await restarted.GetSessionAsync(second, Ct)).Should().NotBeNull();
    }

    [Fact]
    public async Task AnExpiredSessionIsDroppedOnStart()
    {
        var grain = Grain();
        var grant = await grain.OpenSessionAsync(ALICE, Ct);

        _time.Advance(TimeSpan.FromHours(13));
        var restarted = await RestartAsync();

        (await restarted.GetSessionAsync(grant.SessionToken, Ct)).Should().BeNull();
        (await Rows()).Should().BeEmpty();
    }

    [Fact]
    public async Task TheTableHoldsNoUsableToken()
    {
        var grant = await Grain().OpenSessionAsync(ALICE, Ct);
        var row = (await Rows()).Single();

        row.TokenHash.Should().NotContain(grant.SessionToken);
        row.TokenHash.Should().HaveLength(64);
        (await (await RestartAsync()).GetSessionAsync(row.TokenHash, Ct))
            .Should()
            .BeNull("the stored hash is not a token");
    }

    [Fact]
    public async Task APlayersSessionsAreListedAndEndedByIdOnlyByThem()
    {
        var grain = Grain();
        var bobs = await grain.OpenSessionAsync(BOB, Ct);
        var first = await grain.OpenSessionAsync(ALICE, Ct);
        _time.Advance(TimeSpan.FromMinutes(1));
        var second = await grain.OpenSessionAsync(ALICE, Ct);

        (await grain.GetPlayerSessionsAsync(ALICE, Ct))
            .Select(x => x.Id)
            .Should()
            .Equal(second.Session.Id, first.Session.Id);

        (await grain.EndPlayerSessionsAsync(ALICE, bobs.Session.Id, Ct))
            .Should()
            .Be(0, "Bob's session is not Alice's to end");
        (await grain.EndPlayerSessionsAsync(ALICE, first.Session.Id, Ct)).Should().Be(1);

        var restarted = await RestartAsync();

        (await restarted.GetSessionAsync(first.SessionToken, Ct)).Should().BeNull();
        (await restarted.GetSessionAsync(second.SessionToken, Ct)).Should().NotBeNull();
        (await restarted.GetSessionAsync(bobs.SessionToken, Ct)).Should().NotBeNull();

        (await restarted.EndPlayerSessionsAsync(ALICE, null, Ct)).Should().Be(1);
        (await restarted.GetSessionAsync(second.SessionToken, Ct)).Should().BeNull();
        (await restarted.GetSessionAsync(bobs.SessionToken, Ct)).Should().NotBeNull();
    }

    private async Task<List<AdminSessionEntity>> Rows()
    {
        await using var db = await _db.CreateDbContextAsync(Ct);

        return await db.AdminSessions.AsNoTracking().OrderBy(x => x.Id).ToListAsync(Ct);
    }

    /// <summary>The grain as a restarted server brings it up: a new activation over the same rows.</summary>
    private async Task<IAdminAuthGrain> RestartAsync(AdminConfig? config = null)
    {
        var grain = Grain(config);

        await ((Grain)grain).OnActivateAsync(Ct);

        return grain;
    }

    private IAdminAuthGrain Grain(AdminConfig? config = null)
    {
        var type = typeof(AdminConfig).Assembly.GetType("Turbo.Admin.Grains.AdminAuthGrain")!;

        return (IAdminAuthGrain)
            Activator.CreateInstance(
                type,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                [_db, Options.Create(config ?? new AdminConfig()), _time],
                culture: null
            )!;
    }
}
