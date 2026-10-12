using System.Net;
using FluentAssertions;
using Turbo.Admin.Players;
using Turbo.Database.Entities.Furniture;
using Turbo.Database.Entities.Moderation;
using Turbo.Database.Entities.Players;
using Turbo.Primitives.Inventory;
using Turbo.Primitives.Moderation.Enums;
using Turbo.Primitives.Players.Wallet;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Admin;

/// <summary>
/// What staff read about a player in the panel. The hotel kept logins, the currency ledger and
/// furni transfers, and now the moderation history, but nothing read them: no timeline, no
/// way to find a banned player's other accounts. These read them, a page at a time.
/// </summary>
public sealed class AdminPlayerAuditTests : IDisposable
{
    private const int ALICE = 1;
    private const int BOB = 2;
    private const int CAROL = 3;
    private const int DAVE = 4;
    private const int STAFF = 9;

    private static readonly DateTime T0 = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly SqliteDb _db = new();
    private readonly AdminPlayerAudit _audit;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public AdminPlayerAuditTests()
    {
        _db.InsertOwnersAndRooms([ALICE, BOB, CAROL, DAVE, STAFF], [1]);
        _audit = new AdminPlayerAudit(_db);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task The_timeline_merges_every_record_newest_first()
    {
        _db.Insert(Login(ALICE, T0, "10.0.0.1", "m1"));
        _db.Insert(
            new PlayerCurrencyLedgerEntity
            {
                Id = 1,
                PlayerEntityId = ALICE,
                CurrencyTypeId = 1,
                Change = -25,
                BalanceAfter = 75,
                Reason = WalletChangeReason.CatalogPurchase,
                CreatedAt = T0.AddMinutes(1),
            }
        );
        _db.Insert(
            new FurnitureTransferEntity
            {
                Id = 1,
                FurnitureId = 40,
                DefinitionId = FurnitureRows.DEFINITION,
                FromPlayerId = ALICE,
                ToPlayerId = BOB,
                Reason = FurnitureTransferReason.Trade,
                CreatedAt = T0.AddMinutes(2),
            }
        );
        _db.Insert(Action(1, ModerationActionType.Silence, STAFF, ALICE, T0.AddMinutes(3), "spam"));

        var timeline = await _audit.TimelineAsync(ALICE, AllButLogs, null, Ct);

        timeline
            .Events.Select(x => x.Kind)
            .Should()
            .Equal("moderation", "furni", "wallet", "login");
        timeline.Events[0].Summary.Should().Be("Silence by player-9: spam");
        timeline.Events[1].Summary.Should().Be("Gave chair_norja #40 to player-2 (Trade)");
        timeline.Events[2].Summary.Should().StartWith("-25 ");
        timeline.Before.Should().BeNull("one page holds all of it");
    }

    [Fact]
    public async Task The_timeline_leaves_out_what_the_viewer_may_not_read()
    {
        _db.Insert(Login(ALICE, T0, "10.0.0.1", "m1"));

        (await _audit.TimelineAsync(ALICE, TimelineKinds.Moderation, null, Ct))
            .Events.Should()
            .BeEmpty();
    }

    [Fact]
    public async Task The_timeline_pages_by_time()
    {
        for (var i = 0; i < AdminPlayerAudit.PAGE_SIZE + 5; i++)
            _db.Insert(Login(ALICE, T0.AddMinutes(i), "10.0.0.1", "m1", id: i + 1));

        var first = await _audit.TimelineAsync(ALICE, TimelineKinds.Logins, null, Ct);
        var second = await _audit.TimelineAsync(ALICE, TimelineKinds.Logins, first.Before, Ct);

        first.Events.Should().HaveCount(AdminPlayerAudit.PAGE_SIZE);
        second.Events.Should().HaveCount(5);
        second.Events[0].AtUtc.Should().Be(T0.AddMinutes(4));
    }

    [Fact]
    public async Task Alts_put_a_shared_machine_before_a_shared_address_and_say_who_is_banned()
    {
        _db.Insert(Login(ALICE, T0, "10.0.0.1", "machine-a", id: 1));
        _db.Insert(Login(ALICE, T0.AddDays(1), "10.0.0.2", null, id: 2));
        // Bob shares both of Alice's addresses; Carol her machine; Dave nothing.
        _db.Insert(Login(BOB, T0.AddDays(2), "10.0.0.1", "machine-b", id: 3));
        _db.Insert(Login(BOB, T0.AddDays(3), "10.0.0.2", "machine-b", id: 4));
        _db.Insert(Login(CAROL, T0.AddDays(1), "192.168.1.5", "machine-a", id: 5));
        _db.Insert(Login(DAVE, T0, "172.16.0.1", "machine-d", id: 6));
        _db.Insert(
            new PlayerSanctionEntity
            {
                Id = 1,
                PlayerEntityId = CAROL,
                Kind = SanctionKind.Ban,
                Reason = "cheating",
            }
        );

        var alts = await _audit.AltsAsync(ALICE, Ct);

        alts.Select(x => (x.PlayerId, x.SharedAddresses, x.SharedMachine, x.Banned))
            .Should()
            .Equal((CAROL, 0, true, true), (BOB, 2, false, false));
    }

    [Fact]
    public async Task Logins_say_where_from_only_when_the_viewer_may_see_it()
    {
        _db.Insert(Login(ALICE, T0, "10.0.0.1", "machine-a"));

        (await _audit.LoginsAsync(ALICE, identity: false, null, Ct))
            .Single()
            .Should()
            .Match<Turbo.Admin.Api.Contracts.PlayerLoginEntry>(x =>
                x.Address == null && x.Machine == null
            );
        (await _audit.LoginsAsync(ALICE, identity: true, null, Ct))
            .Single()
            .Should()
            .Match<Turbo.Admin.Api.Contracts.PlayerLoginEntry>(x =>
                x.Address == "10.0.0.1" && x.Machine == "machine-a"
            );
    }

    [Fact]
    public async Task The_moderation_log_is_narrowed_named_and_paged_by_id()
    {
        for (var i = 1; i <= AdminPlayerAudit.PAGE_SIZE + 1; i++)
            _db.Insert(
                Action(i, ModerationActionType.RoomKick, STAFF, BOB, T0.AddMinutes(i), null)
            );
        _db.Insert(Action(100, ModerationActionType.Ban, STAFF, ALICE, T0, "spam"));

        var bans = await _audit.ModerationAsync(ALICE, null, null, "ban", null, Ct);
        var kicks = await _audit.ModerationAsync(BOB, STAFF, null, null, null, Ct);
        var older = await _audit.ModerationAsync(BOB, STAFF, null, null, kicks.Before, Ct);

        bans.Actions.Single()
            .Should()
            .Match<Turbo.Admin.Api.Contracts.ModerationActionEntry>(x =>
                x.Action == "Ban"
                && x.ActorName == "player-9"
                && x.TargetName == "player-1"
                && x.Reason == "spam"
            );
        kicks.Actions.Should().HaveCount(AdminPlayerAudit.PAGE_SIZE);
        older.Actions.Select(x => x.Id).Should().Equal(1);
        older.Before.Should().BeNull();
        (await _audit.ModerationAsync(null, null, null, "nonsense", null, Ct))
            .Actions.Should()
            .BeEmpty();
    }

    private const TimelineKinds AllButLogs =
        TimelineKinds.Moderation
        | TimelineKinds.Logins
        | TimelineKinds.Wallet
        | TimelineKinds.Furni
        | TimelineKinds.Rooms;

    private static PlayerLoginEntity Login(
        int player,
        DateTime at,
        string address,
        string? machine,
        int id = 1
    ) =>
        new()
        {
            Id = id,
            PlayerEntityId = player,
            IpAddress = IPAddress.Parse(address),
            MachineId = machine,
            CreatedAt = at,
        };

    private static ModerationActionEntity Action(
        long id,
        ModerationActionType action,
        int actor,
        int target,
        DateTime at,
        string? reason
    ) =>
        new()
        {
            Id = id,
            Action = action,
            Source = ModerationSource.Game,
            ActorId = actor,
            TargetPlayerId = target,
            Reason = reason,
            CreatedAt = at,
        };
}
