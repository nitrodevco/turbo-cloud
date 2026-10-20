using System.Collections.Immutable;
using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Notifications;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.WiredTrading.Enums;
using Turbo.Primitives.WiredTrading.Grains;
using Turbo.Primitives.WiredTrading.Snapshots;
using Turbo.Rooms.Grains;
using Turbo.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Object.Logic.Furniture.Floor.WiredTrading;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A chest's owner is told what happened to it as they asked in its notification settings: a
/// guest's donation or withdrawal, a wired move, the chest filling up or running empty, each a
/// persistent <c>wired_chests.*</c> bubble with the chest's name, who did it and a link to the
/// room; nothing they did not tick, wired-only kinds only on a wired chest, and nothing while
/// they are in the room when they asked for "Only when I am not in the room".
/// </summary>
public class WiredChestNotificationTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private const int CHEST = 7;
    private const int OWNER = 1;
    private const int GUEST = 5;

    [Fact]
    public async Task A_guests_donation_tells_the_owner_with_the_chest_name_and_a_room_link()
    {
        var (room, _) = await CreateChestAsync(wired: false, donation: true);
        StubMove(room, nameof(IWiredChestGrain.DepositAsync), items: 1);

        await room.Room.DepositIntoWiredChestAsync(Guest(), CHEST, [99], Ct);

        var notice = Notices(room).Should().ContainSingle().Subject;
        notice.Args[0].Should().Be((PlayerId)OWNER);
        notice.Args[1].Should().Be("wired_chests.donation");
        var parameters = (ImmutableDictionary<string, string>)notice.Args[2]!;
        parameters["chest_name"].Should().Be("Bank");
        parameters["user_name"].Should().Be($"player{GUEST}");
        parameters["linkUrl"].Should().Be("event:navigator/goto/1");
    }

    [Fact]
    public async Task Nothing_is_sent_for_a_kind_the_owner_did_not_tick()
    {
        var (room, _) = await CreateChestAsync(wired: false, donation: false);
        StubMove(room, nameof(IWiredChestGrain.DepositAsync), items: 1);

        await room.Room.DepositIntoWiredChestAsync(Guest(), CHEST, [99], Ct);

        Notices(room).Should().BeEmpty();
    }

    [Fact]
    public async Task The_owners_own_deposit_is_no_donation()
    {
        var (room, _) = await CreateChestAsync(wired: false, donation: true);
        StubMove(room, nameof(IWiredChestGrain.DepositAsync), items: 1);

        await room.Room.DepositIntoWiredChestAsync(
            ActionContext.CreateForPlayer(OWNER, 1),
            CHEST,
            [99],
            Ct
        );

        Notices(room).Should().BeEmpty();
    }

    [Fact]
    public async Task Only_when_not_in_the_room_holds_back_while_the_owner_is_there()
    {
        var (room, _) = await CreateChestAsync(
            wired: false,
            donation: true,
            mode: WiredChestNotifyMode.OnlyWhenNotInRoom
        );
        StubMove(room, nameof(IWiredChestGrain.DepositAsync), items: 1);

        await room.Room.DepositIntoWiredChestAsync(Guest(), CHEST, [99], Ct);

        Notices(room).Should().BeEmpty();
    }

    [Fact]
    public async Task Only_when_not_in_the_room_tells_an_owner_who_is_away()
    {
        var (room, _) = await CreateChestAsync(
            wired: false,
            donation: true,
            mode: WiredChestNotifyMode.OnlyWhenNotInRoom,
            ownerInRoom: false
        );
        StubMove(room, nameof(IWiredChestGrain.DepositAsync), items: 1);

        await room.Room.DepositIntoWiredChestAsync(Guest(), CHEST, [99], Ct);

        Notices(room).Should().ContainSingle(x => (string)x.Args[1]! == "wired_chests.donation");
    }

    [Fact]
    public async Task A_deposit_that_fills_the_chest_says_it_is_full()
    {
        var (room, _) = await CreateChestAsync(wired: false, donation: false, full: true);
        StubMove(room, nameof(IWiredChestGrain.DepositAsync), items: 1000);

        await room.Room.DepositIntoWiredChestAsync(Guest(), CHEST, [99], Ct);

        Notices(room).Should().ContainSingle(x => (string)x.Args[1]! == "wired_chests.chest_full");
    }

    [Fact]
    public async Task A_wired_give_that_empties_a_wired_chest_says_both()
    {
        var (room, chest) = await CreateChestAsync(
            wired: true,
            donation: false,
            empty: true,
            wiredTransaction: true,
            items: 1
        );
        StubMove(room, nameof(IWiredChestGrain.WithdrawAsync), items: 0);

        await chest.GiveAsync(
            GUEST,
            $"player{GUEST}",
            amount: 1,
            itemType: null,
            WiredChestIterationMode.FirstInFirstOut,
            WiredTransactionType.Wired,
            string.Empty,
            Ct
        );

        Notices(room)
            .Select(x => (string)x.Args[1]!)
            .Should()
            .Equal("wired_chests.chest_wired_transaction", "wired_chests.chest_empty");
    }

    [Fact]
    public async Task Wired_only_kinds_stay_silent_on_a_chest_without_wired()
    {
        var (room, chest) = await CreateChestAsync(
            wired: false,
            donation: false,
            empty: true,
            wiredTransaction: true,
            items: 1
        );
        StubMove(room, nameof(IWiredChestGrain.WithdrawAsync), items: 0);

        await chest.GiveAsync(
            GUEST,
            $"player{GUEST}",
            amount: 1,
            itemType: null,
            WiredChestIterationMode.FirstInFirstOut,
            WiredTransactionType.Wired,
            string.Empty,
            Ct
        );

        Notices(room).Should().BeEmpty();
    }

    private static ActionContext Guest() => ActionContext.CreateForPlayer(GUEST, 1);

    private static List<FakeCall> Notices(RoomHarness room) =>
        room.Fakes.Log.Of(nameof(IPlayerNoticeService.SendPersistentAsync)).ToList();

    private static void StubMove(RoomHarness room, string method, int items) =>
        room.Fakes.Handlers[method] = _ =>
            Task.FromResult(
                new WiredChestMoveResultSnapshot
                {
                    Failure = null,
                    TransactionId = 1,
                    Coins = 0,
                    Items = [],
                    Summary = Summary(items),
                }
            );

    private static WiredChestSummarySnapshot Summary(int items) =>
        WiredChestSummarySnapshot.Empty with
        {
            ItemCount = items,
            MaxCapacity = 1000,
        };

    private static async Task<(RoomHarness Room, FurnitureWiredChestLogic Chest)> CreateChestAsync(
        bool wired,
        bool donation,
        WiredChestNotifyMode mode = WiredChestNotifyMode.Always,
        bool ownerInRoom = true,
        bool full = false,
        bool empty = false,
        bool wiredTransaction = false,
        int items = 0
    )
    {
        var room = new RoomHarness();
        var stream = typeof(RoomGrain).GetField(
            "_roomOutbound",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
        )!;
        stream.SetValue(room.Room, room.Fakes.Create(stream.FieldType, "room-stream"));

        var item = room.CreateFloorItem(
            CHEST,
            2,
            2,
            Altitude.Zero,
            name: "wf_storage_furni1",
            logic: "wired_chest_furni",
            createLogic: (stuffDataFactory, ctx) =>
                new FurnitureWiredFurniChestLogic(stuffDataFactory, ctx)
        );
        room.AddToRoom(item);
        var chest = (FurnitureWiredChestLogic)item.Logic;

        room.Fakes.Handlers["get_PlayerId"] = call =>
            call.Interface == typeof(IRoomPlayer) && call.Key is int id
                ? (PlayerId)id
                : Fakes.NotHandled;
        room.Fakes.Handlers["get_Name"] = call =>
            call.Interface == typeof(IRoomPlayer) && call.Key is int id
                ? $"player{id}"
                : Fakes.NotHandled;
        AddPlayer(room, OWNER);
        AddPlayer(room, GUEST);
        room.Fakes.Handlers[nameof(IWiredChestGrain.GetSummaryAsync)] = _ =>
            Task.FromResult(Summary(items));

        var owner = ActionContext.CreateForPlayer(OWNER, 1);
        await room.Room.InteractWithItemAsync(
            owner,
            CHEST,
            new SetChestPreferencesInteraction
            {
                Name = "Bank",
                Description = "",
                EveryoneCanOpen = true,
                EveryoneCanDonate = true,
                StateMode = default,
                PreviewMode = WiredChestPreviewMode.None,
                PreviewAmount = 1,
                WiredEnabled = wired,
            },
            Ct
        );
        await room.Room.InteractWithItemAsync(
            owner,
            CHEST,
            new SetChestNotificationPreferencesInteraction
            {
                NotifyMode = mode,
                OnChestFull = full,
                OnDonation = donation,
                OnWithdraw = false,
                OnChestEmpty = empty,
                OnWiredTransaction = wiredTransaction,
            },
            Ct
        );
        await chest.SetLockedAsync(false);

        // The summary the chest mirrors before the move.
        await chest.OnAttachAsync(Ct);

        if (!ownerInRoom)
            RemovePlayer(room, OWNER);

        room.Fakes.Log.Calls.Clear();

        return (room, chest);
    }

    private static void AddPlayer(RoomHarness room, int playerId)
    {
        var player = room.Fakes.Create<IRoomPlayer>(playerId);

        (
            (IDictionary<PlayerId, RoomObjectId>)
                RoomHarness.GetMember(room.State, "AvatarsByPlayerId")!
        )[playerId] = playerId;
        (
            (IDictionary<RoomObjectId, IRoomAvatar>)
                RoomHarness.GetMember(room.State, "AvatarsByObjectId")!
        )[playerId] = player;
    }

    private static void RemovePlayer(RoomHarness room, int playerId)
    {
        (
            (IDictionary<PlayerId, RoomObjectId>)
                RoomHarness.GetMember(room.State, "AvatarsByPlayerId")!
        ).Remove(playerId);
        (
            (IDictionary<RoomObjectId, IRoomAvatar>)
                RoomHarness.GetMember(room.State, "AvatarsByObjectId")!
        ).Remove(playerId);
    }
}
