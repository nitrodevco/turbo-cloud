using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Turbo.Admin.Configuration;
using Turbo.Admin.Players;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Security;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Admin;

/// <summary>
/// Players who sign in to the public site with Discord, as staff see and handle them: the Discord
/// account on their page and in the list, found by its name or id; unlinked, which also signs them
/// out of the site; or only signed out.
/// </summary>
public sealed class AdminSiteAccountsTests : IDisposable
{
    private const int ALICE = 1;
    private const int BOB = 2;
    private static readonly DateTime NOW = new(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();
    private readonly ManualTimeProvider _time = new(new DateTimeOffset(NOW));

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public AdminSiteAccountsTests()
    {
        _db.Insert(Player(ALICE, "alice"));
        _db.Insert(Player(BOB, "bob"));
        _db.Insert(
            new PlayerDiscordLinkEntity
            {
                Id = 1,
                PlayerEntityId = ALICE,
                DiscordId = "123456789",
                DiscordUsername = "alice.discord",
                PlayerEntity = null!,
            }
        );
        _db.Insert(Session(1, ALICE, NOW.AddDays(10)));
        _db.Insert(Session(2, ALICE, NOW.AddDays(-1)));
        _db.Insert(Session(3, BOB, NOW.AddDays(10)));
        _fakes.Handlers["GetOnlinePlayerIds"] = _ =>
            (IReadOnlyCollection<Turbo.Primitives.Players.PlayerId>)[];
    }

    public void Dispose() => _db.Dispose();

    private AdminPlayerQueries Queries() =>
        new(
            _db,
            _fakes.Create<Orleans.IGrainFactory>(),
            _fakes.Create<ISessionGateway>(),
            Options.Create(new AdminConfig()),
            _time
        );

    [Fact]
    public async Task APlayersPage_ShowsTheirDiscord_AndTheirLiveSiteSignIns()
    {
        var alice = await Queries().GetAsync(ALICE, Ct);

        alice!.Discord.Should().NotBeNull();
        alice.Discord!.Id.Should().Be("123456789");
        alice.Discord.Username.Should().Be("alice.discord");
        alice.Discord.ActiveSignIns.Should().Be(1, "one of the two has ended");
        (await Queries().GetAsync(BOB, Ct))!.Discord.Should().BeNull();
    }

    [Fact]
    public async Task APlayer_IsFoundByTheirDiscordName_OrId_AndTheListSaysWhoUsesDiscord()
    {
        foreach (var term in new[] { "DISCORD", "123456789" })
            (await Queries().SearchAsync(term, PlayerSearchMode.Discord, false, 1, Ct))
                .Players.Select(x => (x.Name, x.DiscordUsername))
                .Should()
                .Equal(("alice", "alice.discord"));

        (await Queries().SearchAsync("12345", PlayerSearchMode.Discord, false, 1, Ct))
            .Total.Should()
            .Be(0, "an id is matched whole");
        (await Queries().SearchAsync("bob", PlayerSearchMode.Name, false, 1, Ct))
            .Players.Single()
            .DiscordUsername.Should()
            .BeNull();
    }

    [Fact]
    public async Task Unlinking_TakesTheirDiscord_AndSignsThemOutOfTheSite()
    {
        var accounts = new AdminSiteAccounts(_db);

        (await accounts.UnlinkDiscordAsync(ALICE, Ct)).Should().BeTrue();
        (await accounts.UnlinkDiscordAsync(ALICE, Ct)).Should().BeFalse("nothing is linked now");

        await using var db = await _db.CreateDbContextAsync(Ct);

        (await db.PlayerDiscordLinks.CountAsync(Ct)).Should().Be(0);
        (await db.WebSessions.Select(x => x.PlayerEntityId).ToListAsync(Ct)).Should().Equal(BOB);
    }

    [Fact]
    public async Task SigningOutOfTheSite_EndsOnlyThatPlayersSignIns()
    {
        (await new AdminSiteAccounts(_db).EndSessionsAsync(ALICE, Ct)).Should().Be(2);

        await using var db = await _db.CreateDbContextAsync(Ct);

        (await db.WebSessions.Select(x => x.PlayerEntityId).ToListAsync(Ct)).Should().Equal(BOB);
        (await db.PlayerDiscordLinks.CountAsync(Ct)).Should().Be(1, "they keep their Discord");
    }

    private static PlayerEntity Player(int id, string name) =>
        new()
        {
            Id = id,
            Name = name,
            Figure = "hd-180-1",
            Gender = AvatarGenderType.Male,
            PlayerStatus = PlayerStatusType.Offline,
        };

    private static WebSessionEntity Session(int id, int playerId, DateTime expires) =>
        new()
        {
            Id = id,
            TokenHash = new string((char)('a' + id), 64),
            PlayerEntityId = playerId,
            ExpiresAt = expires,
            PlayerEntity = null!,
        };
}
