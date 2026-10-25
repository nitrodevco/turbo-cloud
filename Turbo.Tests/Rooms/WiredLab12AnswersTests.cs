using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Events.Avatar;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// W1/W2 lab answers of 2026-10-24/25 (official client capture, WIN63-202610201032, own room):
/// Move Furni To Furni on a furni already on its destination sends nothing (action.34.R2,
/// 2026-10-24T12-24-29, 14/14), and a step between two tiles of one 2x2 furni fires neither Walks
/// Off nor Walks On (trigger.2.B1, 3/3). Ours sent the furni an ObjectUpdate every firing, and
/// told the furni the user left it and came onto it again.
/// </summary>
public sealed class WiredLab12AnswersTests
{
    private const int CLICK_ME = 20;
    private const int DESTINATION = 21;
    private const int MOVER = 22;
    private const int RUG = 23;
    private const int TRIGGER = 1;
    private const int ACTION = 2;
    private const int AVATAR = 5;

    private readonly WiredRoom _room = new(12, 12);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    [Fact]
    public async Task Furni_to_furni_on_a_furni_already_on_its_destination_sends_nothing_action_34_R2()
    {
        _room.AddFloorItem(DESTINATION, 5, 5);
        var mover = _room.AddFloorItem(MOVER, 7, 7);
        await BuildFurniToFurniAsync();

        // The first firing puts it on the block; every later one finds it there already.
        (await ClickAsync())
            .Should()
            .Be(1);
        (mover.X, mover.Y).Should().Be((5, 5));

        var updatesBefore = _room.Harness.Fakes.SentToRoom<ObjectUpdateMessageComposer>().Count();

        for (var firing = 0; firing < 3; firing++)
            (await ClickAsync()).Should().Be(0);

        _room
            .Harness.Fakes.SentToRoom<ObjectUpdateMessageComposer>()
            .Count()
            .Should()
            .Be(updatesBefore);
        (mover.X, mover.Y).Should().Be((5, 5));
    }

    [Fact]
    public async Task A_step_between_two_tiles_of_one_furni_neither_leaves_nor_enters_it_trigger_2_B1()
    {
        var heard = new List<RoomEvent>();

        _room.Harness.EventListeners.Register([new Recorder(heard)]);
        _room.AddFloorItem(RUG, 4, 4, width: 2, length: 2);
        var avatar = _room.Enter(AVATAR, 3, 4);
        var player = ActionContext.CreateForPlayer((PlayerId)(100 + AVATAR), (RoomId)1);

        // Onto the rug, across it, and off it again.
        foreach (var (x, y) in new[] { (4, 4), (5, 4), (6, 4) })
        {
            (await _room.Harness.Room.WalkAvatarToAsync(player, x, y, Ct)).Should().BeTrue();

            for (var tick = 1; tick <= 6 && avatar.IsWalking; tick++)
            {
                _now += 500;
                await _room.Harness.Room.AvatarTickSystem.ProcessAvatarsAsync(_now, Ct);
            }
        }

        heard
            .Where(e => e is AvatarWalkOnFurniEvent or AvatarWalkOffFurniEvent)
            .Select(e => e is AvatarWalkOnFurniEvent ? "on" : "off")
            .Should()
            .Equal("on", "off");
    }

    private async Task BuildFurniToFurniAsync()
    {
        _room.AddFloorItem(CLICK_ME, 11, 11);
        _room.AddBox<WiredTriggerClickFurni>(TRIGGER, 0, 0, "wf_trg_click_furni");
        (
            await _room.SaveAsync<UpdateTriggerMessage>(
                TRIGGER,
                stuffIds: [CLICK_ME],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ]
            )
        ).Should().BeTrue();
        _room.AddBox<WiredActionFurniToFurni>(ACTION, 0, 0, "wf_act_furni_to_furni");
        (
            await _room.SaveAsync<UpdateActionMessage>(
                ACTION,
                stuffIds: [MOVER],
                stuffIds2: [DESTINATION],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                    [WiredFurniSourceType.SecondaryItems],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();
        await Wired.OnRoomEventAsync(
            new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                StackIds = [_room.Map.ToIdx(0, 0)],
            },
            Ct
        );
        await TickAsync(1);
    }

    /// <summary>How many furni moves one click sent the room.</summary>
    private async Task<int> ClickAsync()
    {
        var before = _room.Harness.Fakes.SentToRoom<WiredMovementsMessageComposer>().Count();

        await _room
            .FloorItem(CLICK_ME)
            .Logic.OnClickAsync(ActionContext.CreateForPlayer((PlayerId)105, (RoomId)1), 0, Ct);
        await TickAsync(3);

        return _room
            .Harness.Fakes.SentToRoom<WiredMovementsMessageComposer>()
            .Skip(before)
            .Sum(p => p.FloorItems.Count);
    }

    private async Task TickAsync(int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            _now += 1_000;
            await Wired.ProcessWiredAsync(_now, dormant: false, Ct);
        }
    }

    private sealed class Recorder(List<RoomEvent> heard) : IRoomEventListener
    {
        public Task OnRoomEventAsync(RoomEvent evt, CancellationToken ct)
        {
            heard.Add(evt);

            return Task.CompletedTask;
        }
    }
}
