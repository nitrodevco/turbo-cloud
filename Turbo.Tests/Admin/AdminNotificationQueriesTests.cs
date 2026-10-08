using FluentAssertions;
using Microsoft.Extensions.Options;
using Turbo.Admin.Configuration;
using Turbo.Admin.Notifications;
using Turbo.Database.Entities.Moderation;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Room;
using Turbo.Primitives.Availability;
using Turbo.Primitives.Moderation.Enums;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Admin;

/// <summary>
/// The panel's bell: maintenance or a shutdown first, then the week's bans and refused commands,
/// newest first, each kind only for staff who may see it.
/// </summary>
public sealed class AdminNotificationQueriesTests : IDisposable
{
    private const int ALICE = 1;
    private const int MOD = 2;

    private static readonly DateTime NOW = new(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);
    private static readonly NotificationScope EVERYTHING = new(Bans: true, RefusedCommands: true);

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();
    private readonly ManualTimeProvider _time = new(new DateTimeOffset(NOW));
    private HotelAvailabilitySnapshot _availability = HotelAvailabilitySnapshot.Open;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public AdminNotificationQueriesTests()
    {
        _db.Insert(Player(ALICE, "alice"));
        _db.Insert(Player(MOD, "moderator"));

        _db.Insert(Ban(1, "spamming", issuer: MOD, at: NOW.AddHours(-2), expires: NOW.AddDays(1)));
        _db.Insert(Ban(2, "scamming", issuer: null, at: NOW.AddDays(-1), expires: null));
        _db.Insert(Ban(3, "too long ago", issuer: MOD, at: NOW.AddDays(-8), expires: null));

        _db.Insert(Log(10, ALICE, "ban", "moderator", "refused", NOW.AddHours(-1)));
        _db.Insert(Log(11, ALICE, "kick", "bob", "room_level", NOW.AddHours(-3)));
        _db.Insert(Log(12, 0, "shutdown", "", "refused", NOW.AddHours(-4)));
        _db.Insert(Log(13, MOD, "ban", "alice", "completed", NOW.AddHours(-5)));
        _db.Insert(Log(14, ALICE, "ban", "moderator", "refused", NOW.AddDays(-9)));

        _fakes.Handlers["get_Current"] = _ => _availability;
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task TheWeeksBansAndRefusedCommands_AreListed_NewestFirst()
    {
        var items = (await Queries().GetAsync(EVERYTHING, Ct)).Items;

        items
            .Select(x => x.Id)
            .Should()
            .Equal("refusedCommand:10", "ban:1", "refusedCommand:11", "refusedCommand:12", "ban:2");
    }

    [Fact]
    public async Task ABan_SaysWhoBannedWhom_WhyAndUntilWhen()
    {
        var items = (await Queries().GetAsync(EVERYTHING, Ct)).Items;

        var timed = items.Single(x => x.Id == "ban:1");
        timed.Title.Should().Be("moderator banned alice");
        timed.Detail.Should().Be("spamming · until 2026-10-08 12:00 UTC");
        timed.PlayerId.Should().Be(ALICE);

        var permanent = items.Single(x => x.Id == "ban:2");
        permanent.Title.Should().Be("The server banned alice");
        permanent.Detail.Should().Be("scamming · permanent");
    }

    [Fact]
    public async Task ARefusedCommand_SaysWhoTriedWhat()
    {
        var items = (await Queries().GetAsync(EVERYTHING, Ct)).Items;

        var refused = items.Single(x => x.Id == "refusedCommand:10");
        refused.Title.Should().Be("alice was refused :ban");
        refused.Detail.Should().Be(":ban moderator");
        refused.PlayerId.Should().Be(ALICE);

        var console = items.Single(x => x.Id == "refusedCommand:12");
        console.Title.Should().Be("The console was refused :shutdown");
        console.Detail.Should().BeNull();
        console.PlayerId.Should().BeNull();
    }

    [Fact]
    public async Task EachKind_IsOnlyForStaffWhoMaySeeIt()
    {
        (await Queries().GetAsync(new NotificationScope(Bans: true, RefusedCommands: false), Ct))
            .Items.Select(x => x.Kind)
            .Should()
            .OnlyContain(x => x == "ban");
        (await Queries().GetAsync(new NotificationScope(Bans: false, RefusedCommands: true), Ct))
            .Items.Select(x => x.Kind)
            .Should()
            .OnlyContain(x => x == "refusedCommand");
        (await Queries().GetAsync(new NotificationScope(false, false), Ct))
            .Items.Should()
            .BeEmpty();
    }

    [Fact]
    public async Task ScheduledMaintenance_IsFirst_ForEveryone()
    {
        var start = NOW.AddMinutes(10);
        _availability = new HotelAvailabilitySnapshot(
            HotelAvailabilityPhase.MaintenanceScheduled,
            start,
            "Updating the hotel"
        );

        var items = (await Queries().GetAsync(new NotificationScope(false, false), Ct)).Items;

        items.Should().ContainSingle();
        items[0].Kind.Should().Be("availability");
        items[0].Title.Should().Be("Maintenance is scheduled");
        items[0].Detail.Should().Be("Updating the hotel");
        items[0].AtUtc.Should().Be(start);
        (await Queries().GetAsync(EVERYTHING, Ct))
            .Items[0]
            .Id.Should()
            .Be(items[0].Id, "the same maintenance keeps its id");
    }

    private AdminNotificationQueries Queries() =>
        new(_db, _fakes.Create<IHotelAvailability>(), Options.Create(new AdminConfig()), _time);

    private static PlayerEntity Player(int id, string name) =>
        new()
        {
            Id = id,
            Name = name,
            Figure = "hd-180-1",
            Gender = AvatarGenderType.Male,
            PlayerStatus = PlayerStatusType.Offline,
        };

    private static PlayerSanctionEntity Ban(
        int id,
        string reason,
        int? issuer,
        DateTime at,
        DateTime? expires
    ) =>
        new()
        {
            Id = id,
            PlayerEntityId = ALICE,
            Kind = SanctionKind.Ban,
            Reason = reason,
            IssuerEntityId = issuer,
            ExpiresAt = expires,
            CreatedAt = at,
        };

    private static CommandLogEntity Log(
        int id,
        int playerId,
        string command,
        string arguments,
        string outcome,
        DateTime at
    ) =>
        new()
        {
            Id = id,
            PlayerEntityId = playerId,
            RoomEntityId = 0,
            Command = command,
            Arguments = arguments,
            Outcome = outcome,
            CreatedAt = at,
        };
}
