using System.Collections.Immutable;
using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Grains;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The wired menu's Variable Management, offered for a permanent user variable
/// (<c>WiredMenuOverviewTab.canManageCurrentVariable</c>, WIN63-202610201032): "Manage" asks for
/// the first page of its holders (<c>WiredGetVariableOwnersPage(id, 1, 50, 0, -1)</c>, answered by
/// <c>WiredUserVariablesPage</c>), a holder's "Manage" for what that user holds
/// (<c>WiredGetUserPermanentVariables(type, id)</c>, answered by
/// <c>WiredUserPermanentVariablesList</c>), and the detail window edits, gives and takes values
/// (<c>WiredSetUserPermanentVariable(type, id, variable, value, 0|1|2)</c>, answered by a success
/// flag). The server read none of these requests, so the window never opened.
/// </summary>
public sealed class WiredVariableManagementTests
{
    private const int COINS_BOX = 3;
    private const int LEVEL_BOX = 4;
    private const int VISIT_BOX = 5;
    private const int ALICE_INDEX = 6;
    private const int BOB_INDEX = 7;

    // WiredRoom seats every player as 100 + their room index.
    private const int ALICE = 100 + ALICE_INDEX;
    private const int BOB = 100 + BOB_INDEX;
    private const int USER = (int)RoomObjectType.Player;

    private readonly WiredRoom _room = new(8, 8);
    private long _nowMs;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    private static ActionContext Owner => ActionContext.CreateForSystem(1);

    public WiredVariableManagementTests()
    {
        _room.Harness.Fakes.Handlers["GetPlayerNamesAsync"] = call =>
            Task.FromResult(
                ((List<PlayerId>)call.Args[0]!).ToImmutableDictionary(
                    id => id,
                    id => id.Value == BOB ? "bob" : "someone"
                )
            );
        _room.Harness.Fakes.Handlers["GetSummaryAsync"] = call =>
            call.Interface == typeof(IPlayerGrain)
                ? Task.FromResult(Summary(Convert.ToInt32(call.Key)))
                : Fakes.NotHandled;
    }

    [Fact]
    public void The_three_requests_read_as_the_client_writes_them()
    {
        PacketHarness
            .Parse(
                PacketHarness.Incoming("WiredGetVariableOwnersPageMessageEvent"),
                PacketHarness.Payload(w => w.String("123").Int(1).Int(50).Int(0).Int(-1))
            )
            .Should()
            .BeEquivalentTo(
                new WiredGetVariableOwnersPageMessage
                {
                    VariableId = "123",
                    Page = 1,
                    PageSize = 50,
                    SortType = 0,
                    UserTypeFilter = -1,
                }
            );
        PacketHarness
            .Parse(
                PacketHarness.Incoming("WiredGetUserPermanentVariablesMessageEvent"),
                PacketHarness.Payload(w => w.Int(1).Int(BOB))
            )
            .Should()
            .BeEquivalentTo(
                new WiredGetUserPermanentVariablesMessage { EntityType = 1, EntityId = BOB }
            );
        PacketHarness
            .Parse(
                PacketHarness.Incoming("WiredSetUserPermanentVariableMessageEvent"),
                PacketHarness.Payload(w => w.Int(1).Int(BOB).String("123").Int(7).Int(2))
            )
            .Should()
            .BeEquivalentTo(
                new WiredSetUserPermanentVariableMessage
                {
                    EntityType = 1,
                    EntityId = BOB,
                    VariableId = "123",
                    Value = 7,
                    Operation = 2,
                }
            );
    }

    [Fact]
    public async Task Manage_lists_holders_here_and_away_by_player_id_sorted_and_filtered()
    {
        var coins = await SetUpAsync();

        var page = await _room.Harness.Room.GetWiredVariableOwnersPageAsync(
            Owner,
            coins,
            1,
            50,
            0,
            -1,
            Ct
        );

        page.Should().NotBeNull();
        page!.VariableId.Should().Be(coins.ToString());
        page.TotalEntries.Should().Be(2);
        page.Page.Should().Be(1);
        page.PageSize.Should().Be(50);
        page.UserTypeFilter.Should().Be(-1);
        page.SortType.Should().Be(0);
        // Highest value first: bob (away) holds 9, alice (here) 5.
        page.Owners.Select(x => (x.EntityType, x.EntityId, x.EntityName, x.Value.Value))
            .Should()
            .Equal((USER, BOB, "bob", 9), (USER, ALICE, "alice", 5));
        // Habbo prints the times as "20-10-2026 22:52:00" (official client capture).
        page.Owners[0].Value.CreatedAtMs.Should().BeGreaterThan(0);
        page.Owners[0].Value.CreatedAtText.Should().MatchRegex(@"^\d\d-\d\d-\d{4} \d\d:\d\d:\d\d$");

        var lowest = await Wired.GetVariableOwnersPageAsync(coins, 1, 50, 1, -1, Ct);

        lowest!.Owners.Select(x => x.EntityId).Should().Equal(ALICE, BOB);

        var pets = await Wired.GetVariableOwnersPageAsync(
            coins,
            1,
            50,
            0,
            (int)RoomObjectType.Pet,
            Ct
        );

        pets!.TotalEntries.Should().Be(0);
        pets.UserTypeFilter.Should().Be((int)RoomObjectType.Pet);

        var packet = PacketHarness.Encode(
            new WiredUserVariablesListMessageComposer { Page = page }
        );

        // WiredUserVariablesPage, each holder a WiredUserVariablesElement whose value has no id.
        packet.Header.Should().Be(PacketHarness.Outgoing("WiredUserVariablesListMessageComposer"));
        packet.PopString().Should().Be(coins.ToString());
        packet.PopInt().Should().Be(2);
        packet.PopInt().Should().Be(1);
        packet.PopInt().Should().Be(50);
        packet.PopInt().Should().Be(2);
        packet.PopInt().Should().Be(USER);
        packet.PopInt().Should().Be(BOB);
        packet.PopString().Should().Be("bob");
        packet.PopInt().Should().Be(9);
        packet.PopLong().Should().Be(page.Owners[0].Value.CreatedAtMs);
        packet.PopString().Should().Be(page.Owners[0].Value.CreatedAtText);
        packet.PopLong().Should().Be(page.Owners[0].Value.UpdatedAtMs);
        packet.PopString().Should().Be(page.Owners[0].Value.UpdatedAtText);
        packet.PopInt().Should().Be(USER);
        packet.PopInt().Should().Be(ALICE);
        packet.PopString().Should().Be("alice");
        packet.PopInt().Should().Be(5);
        packet.PopLong();
        packet.PopString();
        packet.PopLong();
        packet.PopString();
        packet.PopInt().Should().Be(-1);
        packet.PopInt().Should().Be(0);
        packet.End.Should().BeTrue();
    }

    [Fact]
    public async Task A_variable_that_is_not_kept_beyond_the_visit_is_not_managed()
    {
        await SetUpAsync();

        var visit = await AddUserVariableAsync(
            VISIT_BOX,
            3,
            3,
            WiredAvailabilityType.UserActive,
            "visit"
        );

        (await Wired.GetVariableOwnersPageAsync(visit, 1, 50, 0, -1, Ct)).Should().BeNull();
        (
            await Wired.ApplyVariableManagementChangeAsync(
                USER,
                ALICE,
                visit,
                WiredVariableMenuOperationType.Create,
                new WiredVariableValue(1),
                Ct
            )
        )
            .Should()
            .BeFalse();
    }

    [Fact]
    public async Task A_holders_detail_names_an_away_player_and_lists_their_permanent_values()
    {
        var coins = await SetUpAsync();

        var holder = await _room.Harness.Room.GetWiredEntityPermanentVariablesAsync(
            Owner,
            USER,
            BOB,
            Ct
        );

        holder.Should().NotBeNull();
        holder!.EntityName.Should().Be("bob");
        holder.EntityFigure.Should().Be("hd-180-1");
        holder.Variables.Select(x => (x.VariableId, x.Value)).Should().Equal((coins.ToString(), 9));

        var packet = PacketHarness.Encode(
            new WiredUserPermanentVariablesMessageComposer { Holder = holder }
        );

        // WiredUserPermanentVariablesList: no owner block for a user; each value with its id.
        packet
            .Header.Should()
            .Be(PacketHarness.Outgoing("WiredUserPermanentVariablesMessageComposer"));
        packet.PopInt().Should().Be(USER);
        packet.PopInt().Should().Be(BOB);
        packet.PopString().Should().Be("bob");
        packet.PopString().Should().Be("hd-180-1");
        packet.PopInt().Should().Be(1);
        packet.PopString().Should().Be(coins.ToString());
        packet.PopInt().Should().Be(9);
        packet.PopLong();
        packet.PopString();
        packet.PopLong();
        packet.PopString();
        packet.End.Should().BeTrue();

        // Somebody who holds nothing permanent here and is not here is nobody to the window.
        (await Wired.GetEntityPermanentVariablesAsync(USER, 999, Ct))
            .Should()
            .BeNull();
    }

    [Fact]
    public void A_pet_or_bot_holder_names_its_owner()
    {
        var packet = PacketHarness.Encode(
            new WiredUserPermanentVariablesMessageComposer
            {
                Holder = new WiredEntityPermanentVariablesSnapshot
                {
                    EntityType = (int)RoomObjectType.Pet,
                    EntityId = 12,
                    EntityName = "rex",
                    EntityFigure = "0 0 ffffff",
                    OwnerId = ALICE,
                    OwnerName = "alice",
                    OwnerFigure = "hd-600-1",
                    Variables = [],
                },
            }
        );

        packet.PopInt().Should().Be((int)RoomObjectType.Pet);
        packet.PopInt().Should().Be(12);
        packet.PopString().Should().Be("rex");
        packet.PopString().Should().Be("0 0 ffffff");
        packet.PopInt().Should().Be(ALICE);
        packet.PopString().Should().Be("alice");
        packet.PopString().Should().Be("hd-600-1");
        packet.PopInt().Should().Be(0);
        packet.End.Should().BeTrue();
    }

    [Fact]
    public async Task The_detail_window_edits_gives_and_takes_values_of_a_player_who_is_away()
    {
        var coins = await SetUpAsync();
        var level = await AddUserVariableAsync(
            LEVEL_BOX,
            4,
            4,
            WiredAvailabilityType.Persistent,
            "level"
        );

        (await ChangeAsync(coins, WiredVariableMenuOperationType.SetValue, 12)).Should().BeTrue();
        (await ChangeAsync(level, WiredVariableMenuOperationType.Create, 3)).Should().BeTrue();

        (await Values(BOB))
            .Should()
            .BeEquivalentTo([(coins.ToString(), 12), (level.ToString(), 3)]);

        (await ChangeAsync(coins, WiredVariableMenuOperationType.Delete, 0)).Should().BeTrue();

        (await Values(BOB)).Should().Equal((level.ToString(), 3));
        // An edit needs a value already held.
        (await ChangeAsync(coins, WiredVariableMenuOperationType.SetValue, 1))
            .Should()
            .BeFalse();
    }

    [Fact]
    public async Task Someone_who_may_not_modify_the_rooms_wired_changes_nothing()
    {
        var coins = await SetUpAsync();

        (
            await _room.Harness.Room.ApplyWiredVariableManagementChangeAsync(
                ActionContext.CreateForPlayer(ALICE, 1),
                USER,
                BOB,
                coins,
                WiredVariableMenuOperationType.SetValue,
                new WiredVariableValue(100),
                Ct
            )
        )
            .Should()
            .BeFalse();

        (await Values(BOB)).Should().Equal((coins.ToString(), 9));
    }

    [Fact]
    public async Task A_change_is_answered_with_its_result_and_the_holders_values_as_they_are_now()
    {
        var harness = new PacketHarness();
        var holder = new WiredEntityPermanentVariablesSnapshot
        {
            EntityType = USER,
            EntityId = BOB,
            EntityName = "bob",
            EntityFigure = "hd-180-1",
            Variables = [],
        };

        harness.Fakes.Handlers["ApplyWiredVariableManagementChangeAsync"] = _ =>
            Task.FromResult(true);
        harness.Fakes.Handlers["GetWiredEntityPermanentVariablesAsync"] = _ =>
            Task.FromResult<WiredEntityPermanentVariablesSnapshot?>(holder);

        var replies = await harness.SendAsync(
            PacketHarness.Incoming("WiredSetUserPermanentVariableMessageEvent"),
            PacketHarness.Payload(w => w.Int(USER).Int(BOB).String("123").Int(7).Int(0)),
            playerId: 1,
            roomId: 1
        );

        replies
            .Select(x => x.Header)
            .Should()
            .Equal(
                PacketHarness.Outgoing("WiredSetUserPermanentVariableResultMessageComposer"),
                PacketHarness.Outgoing("WiredUserPermanentVariablesMessageComposer")
            );
        replies[0].PopBoolean().Should().BeTrue();
        replies[0].End.Should().BeTrue();

        var change = harness.Fakes.Log.Of("ApplyWiredVariableManagementChangeAsync").Single();

        change.Args[1].Should().Be(USER);
        change.Args[2].Should().Be(BOB);
        change.Args[4].Should().Be(WiredVariableMenuOperationType.SetValue);
        change.Args[5].Should().Be(new WiredVariableValue(7));
    }

    /// <summary>
    /// A permanent "coins" user variable: alice is here holding 5, bob held 9 and left, so the
    /// box keeps his value under his player id.
    /// </summary>
    private async Task<WiredVariableId> SetUpAsync()
    {
        _room.Enter(ALICE_INDEX, 1, 1, "alice");
        _room.Enter(BOB_INDEX, 1, 1, "bob");

        var coins = await AddUserVariableAsync(
            COINS_BOX,
            5,
            5,
            WiredAvailabilityType.Persistent,
            "coins"
        );

        (await GiveAsync(coins, ALICE, 5)).Should().BeTrue();
        (await GiveAsync(coins, BOB, 9)).Should().BeTrue();
        Leave(BOB_INDEX);

        return coins;
    }

    private Task<bool> GiveAsync(WiredVariableId variable, int playerId, int value) =>
        Wired.ApplyVariableManagementChangeAsync(
            USER,
            playerId,
            variable,
            WiredVariableMenuOperationType.Create,
            new WiredVariableValue(value),
            Ct
        );

    private Task<bool> ChangeAsync(
        WiredVariableId variable,
        WiredVariableMenuOperationType operation,
        int value
    ) =>
        _room.Harness.Room.ApplyWiredVariableManagementChangeAsync(
            Owner,
            USER,
            BOB,
            variable,
            operation,
            new WiredVariableValue(value),
            Ct
        );

    private async Task<List<(string, int)>> Values(int playerId) =>
        [
            .. (await Wired.GetEntityPermanentVariablesAsync(USER, playerId, Ct))!.Variables.Select(
                x => (x.VariableId, x.Value)
            ),
        ];

    private void Leave(int roomIndex)
    {
        _room.Harness.State.AvatarsByObjectId.Remove(roomIndex);
        _room.Harness.State.AvatarsByPlayerId.Remove(100 + roomIndex);
    }

    private async Task<WiredVariableId> AddUserVariableAsync(
        int id,
        int x,
        int y,
        WiredAvailabilityType availability,
        string name
    )
    {
        var box = _room.AddBox<WiredVariableUser>(id, x, y, "wf_var_user");

        (
            await _room.SaveAsync<UpdateVariableMessage>(
                id,
                intParams: [(int)availability, 1],
                stringParam: name
            )
        )
            .Should()
            .BeTrue();
        await box.LoadWiredAsync(Ct);
        await Wired.OnRoomEventAsync(
            new Turbo.Primitives.Rooms.Events.Wired.RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                StackIds = [_room.Map.ToIdx(x, y)],
            },
            Ct
        );
        await Wired.ProcessWiredAsync(_nowMs += 10_000, dormant: false, Ct);

        return box.GetVarSnapshot().VariableId;
    }

    private static PlayerSummarySnapshot Summary(int playerId) =>
        new()
        {
            PlayerId = playerId,
            Name = playerId == BOB ? "bob" : "someone",
            Motto = string.Empty,
            Figure = "hd-180-1",
            Gender = AvatarGenderType.Male,
            AchievementScore = 0,
            BadgesRank = 0,
            IsOnline = false,
            CreatedAt = DateTime.UtcNow,
            LastUpdated = DateTime.UtcNow,
            RespectPoints = 0,
            RespectsLeft = 0,
            PetRespectsLeft = 0,
            RespectReplenishesLeft = 0,
        };
}
