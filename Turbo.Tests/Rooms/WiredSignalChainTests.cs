using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Selectors;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A hotel's signal build, run through the room's real wired system: a user walks onto a furni, a
/// selector picks every user in the room, "send signal" forwards them to an antenna, and a second
/// stack that receives the signal teleports the users it carried onto a furni. Boxes are saved the
/// way the client's editor saves them, stacks form from the boxes on a tile, and the room's wired
/// tick runs the events and the scheduled actions.
/// </summary>
public sealed class WiredSignalChainTests
{
    private const int CLICK_ME = 23;
    private const int WALK_ON = 20;
    private const int ANTENNA = 21;
    private const int TARGET = 22;

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    public WiredSignalChainTests()
    {
        _room.Enter(5, 1, 1);
        _room.Enter(6, 1, 3);
        _room.Enter(7, 3, 1);
        _room.AddFloorItem(WALK_ON, 6, 6);
        _room.AddFloorItem(CLICK_ME, 7, 4);
        _room.AddFloorItem(ANTENNA, 6, 1);
        _room.AddFloorItem(TARGET, 5, 5);
    }

    [Fact]
    public async Task EveryUserTheSelectorPicked_IsTeleportedByTheStackThatReceivesTheSignal()
    {
        await BuildSenderAsync(sendersUsers: WiredPlayerSourceType.SelectorUsers);
        await BuildReceiverAsync(receiversUsers: WiredPlayerSourceType.SignalUsers);
        await StartAsync();

        await WalkOntoAsync(5);
        await TickAsync(6);

        _room.Positions().Should().Equal("5@5,5", "6@5,5", "7@5,5");
    }

    [Fact]
    public async Task OnlyTheTriggerer_WhenTheSenderForwardsTheTriggeredUser()
    {
        await BuildSenderAsync(sendersUsers: WiredPlayerSourceType.TriggeredUser);
        await BuildReceiverAsync(receiversUsers: WiredPlayerSourceType.SignalUsers);
        await StartAsync();

        await WalkOntoAsync(5);
        await TickAsync(6);

        // 5 walked onto the furni, so it is the only user the signal carries.
        _room.Positions().Should().Equal("5@5,5", "6@1,3", "7@3,1");
    }

    [Fact]
    public async Task OnlyTheTriggerer_WhenTheReceiverUsesTheTriggeredUserInsteadOfTheSignalsUsers()
    {
        await BuildSenderAsync(sendersUsers: WiredPlayerSourceType.SelectorUsers);
        await BuildReceiverAsync(receiversUsers: WiredPlayerSourceType.TriggeredUser);
        await StartAsync();

        await WalkOntoAsync(5);
        await TickAsync(6);

        // Nobody triggered the receiving stack (a signal is not a user), so nobody is teleported:
        // 5 only stands where it walked, on the furni that started the chain.
        _room.Positions().Should().Equal("5@6,6", "6@1,3", "7@3,1");
    }

    // --- the build from the screenshots: a click starts it ---

    [Fact]
    public async Task AUserClickingTheFurni_TeleportsEveryUserTheSelectorPicked()
    {
        await BuildSenderAsync(WiredPlayerSourceType.SelectorUsers, clickTrigger: true);
        await BuildReceiverAsync(receiversUsers: WiredPlayerSourceType.SignalUsers);
        await StartAsync();

        await ClickAsync(6);
        await TickAsync(6);

        _room.Positions().Should().Equal("5@5,5", "6@5,5", "7@5,5");
    }

    [Fact]
    public async Task AUserClickingTheFurni_WithSignalForEachUser_StillTeleportsThemAll()
    {
        await BuildSenderAsync(
            WiredPlayerSourceType.SelectorUsers,
            clickTrigger: true,
            splitUsers: true
        );
        await BuildReceiverAsync(receiversUsers: WiredPlayerSourceType.SignalUsers);
        await StartAsync();

        await ClickAsync(6);
        await TickAsync(10);

        // One signal per user, each teleporting the one it carries.
        _room.Positions().Should().Equal("5@5,5", "6@5,5", "7@5,5");
    }

    [Fact]
    public async Task AUserClickingTheFurni_WithTheSelectorFilteringTheExistingSelection_TeleportsOnlyTheClicker()
    {
        await BuildSenderAsync(
            WiredPlayerSourceType.SelectorUsers,
            clickTrigger: true,
            filterExisting: true
        );
        await BuildReceiverAsync(receiversUsers: WiredPlayerSourceType.SignalUsers);
        await StartAsync();

        await ClickAsync(6);
        await TickAsync(6);

        // Filtering narrows what the trigger selected (the clicker) to the players: just 6.
        _room.Positions().Should().Equal("5@1,1", "6@5,5", "7@3,1");
    }

    [Fact]
    public async Task AUserClickingTheFurni_WithTheSelectorInverted_TeleportsNobodyWhenEveryoneIsAPlayer()
    {
        await BuildSenderAsync(
            WiredPlayerSourceType.SelectorUsers,
            clickTrigger: true,
            invert: true
        );
        await BuildReceiverAsync(receiversUsers: WiredPlayerSourceType.SignalUsers);
        await StartAsync();

        await ClickAsync(6);
        await TickAsync(6);

        _room.Positions().Should().Equal("5@1,1", "6@1,3", "7@3,1");
    }

    [Fact]
    public async Task NothingHappens_WhenNobodyWalksOntoTheFurni()
    {
        await BuildSenderAsync(sendersUsers: WiredPlayerSourceType.SelectorUsers);
        await BuildReceiverAsync(receiversUsers: WiredPlayerSourceType.SignalUsers);
        await StartAsync();

        await TickAsync(6);

        _room.Positions().Should().Equal("5@1,1", "6@1,3", "7@3,1");
    }

    /// <summary>
    /// Stack one, on tile (0,0): a trigger, "users by type" (players), and send signal. The
    /// options are the ones the client's editor offers: the selector's "filter existing selection"
    /// and "invert", and the signal's "for each furni" and "for each user".
    /// </summary>
    private async Task BuildSenderAsync(
        WiredPlayerSourceType sendersUsers,
        bool clickTrigger = false,
        bool filterExisting = false,
        bool invert = false,
        bool splitUsers = false,
        bool splitFurni = false
    )
    {
        if (clickTrigger)
        {
            _room.AddBox<WiredTriggerClickFurni>(1, 0, 0, "wf_trg_click_furni");
        }
        else
        {
            _room.AddBox<WiredTriggerWalkOnFurni>(1, 0, 0, "wf_trg_walks_on_furni");
        }

        _room.AddBox<WiredSelectorEntitiesByType>(2, 0, 0, "wf_slc_users_bytype");
        _room.AddBox<WiredActionSendSignal>(3, 0, 0, "wf_act_send_signal");

        (
            await _room.SaveAsync<UpdateTriggerMessage>(
                1,
                stuffIds: [clickTrigger ? CLICK_ME : WALK_ON],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ]
            )
        ).Should().BeTrue();
        (
            await _room.SaveAsync<UpdateSelectorMessage>(
                2,
                intParams: [1],
                definitionSpecifics: [filterExisting, invert]
            )
        )
            .Should()
            .BeTrue();
        (
            await _room.SaveAsync<UpdateActionMessage>(
                3,
                intParams: [splitFurni ? 1 : 0, splitUsers ? 1 : 0],
                stuffIds: [ANTENNA],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                    [WiredFurniSourceType.SelectedItems],
                ],
                playerSources:
                [
                    [sendersUsers],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();
    }

    /// <summary>Stack two, on tile (0,4): receives the signal, teleports users onto the target.</summary>
    private async Task BuildReceiverAsync(WiredPlayerSourceType receiversUsers)
    {
        _room.AddBox<WiredTriggerReceiveSignal>(4, 0, 4, "wf_trg_recv_signal");
        _room.AddBox<WiredActionTeleportTo>(5, 0, 4, "wf_act_teleport_to");

        (
            await _room.SaveAsync<UpdateTriggerMessage>(
                4,
                stuffIds: [ANTENNA],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ]
            )
        ).Should().BeTrue();
        (
            await _room.SaveAsync<UpdateActionMessage>(
                5,
                intParams: [0],
                stuffIds: [TARGET],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ],
                playerSources:
                [
                    [receiversUsers],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();
    }

    /// <summary>The room builds its stacks on the next tick after a box says its stack changed.</summary>
    private async Task StartAsync()
    {
        await Wired.OnRoomEventAsync(
            new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                StackIds = [_room.Map.ToIdx(0, 0), _room.Map.ToIdx(0, 4)],
            },
            Ct
        );

        await TickAsync(1);
    }

    /// <summary>A user clicks the furni: what the client's click packet becomes in the room.</summary>
    private Task ClickAsync(int objectId) =>
        _room
            .FloorItem(CLICK_ME)
            .Logic.OnClickAsync(
                ActionContext.CreateForPlayer((PlayerId)(100 + objectId), (RoomId)1),
                0,
                Ct
            );

    private Task WalkOntoAsync(int objectId) =>
        _room
            .Harness.Module<RoomAvatarModule>()
            .RelocateAvatarAsync(_room.Avatars[objectId], _room.Map.ToIdx(6, 6), Ct);

    /// <summary>
    /// A tick runs what was scheduled, then the events queued; the trigger, the signal and the
    /// teleport are three steps, so a few ticks carry a chain through.
    /// </summary>
    private async Task TickAsync(int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            _now += 1_000;
            await Wired.ProcessWiredAsync(_now, dormant: false, Ct);
        }
    }
}
