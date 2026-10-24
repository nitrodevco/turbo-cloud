using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Set Altitude and Furni To Furni keep a picked wall item and, on every firing, send it in their
/// WiredMovements as a wall move whose source is its target: the wall item stays where it is
/// (official client capture 2026-10-24T17-49-51, WIN63-202610201032, wired matrix action.29.B11
/// and action.34.B11 (wall item onto block K), 2/2 each).
/// </summary>
public sealed class WiredWallItemInPlaceTests
{
    private const int TRIGGER = 1;
    private const int ACTION = 2;
    private const int CLICK_ME = 20;
    private const int BLOCK = 21;
    private const int WALL_ITEM = 70;

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    [Fact]
    public async Task Set_altitude_sends_a_picked_wall_item_where_it_is()
    {
        _room.AddBox<WiredActionSetAltitude>(ACTION, 7, 7, "wf_act_set_altitude");
        await BuildAsync(intParams: [100, 0], stuffIds2: null);

        await ClickTwiceAndExpectTheWallItemInPlaceAsync();
    }

    [Fact]
    public async Task Furni_to_furni_sends_a_picked_wall_item_where_it_is()
    {
        _room.AddBox<WiredActionFurniToFurni>(ACTION, 7, 7, "wf_act_furni_to_furni");
        await BuildAsync(intParams: [], stuffIds2: [BLOCK]);

        await ClickTwiceAndExpectTheWallItemInPlaceAsync();
    }

    private async Task ClickTwiceAndExpectTheWallItemInPlaceAsync()
    {
        var wall = _room.Harness.State.ItemsById[WALL_ITEM];
        var (x, y) = (wall.X, wall.Y);

        await ClickAsync();
        await ClickAsync();

        var moves = _room
            .Harness.Fakes.SentToRoom<WiredMovementsMessageComposer>()
            .SelectMany(m => m.WallItems)
            .ToList();

        moves.Should().HaveCount(2);
        moves
            .Should()
            .OnlyContain(m =>
                m.ObjectId == WALL_ITEM
                && m.SourceX == x
                && m.TargetX == x
                && m.SourceY == y
                && m.TargetY == y
                && m.SourceOffsetX == m.TargetOffsetX
                && m.SourceOffsetY == m.TargetOffsetY
            );
    }

    private async Task BuildAsync(int[] intParams, int[]? stuffIds2)
    {
        _room.AddFloorItem(CLICK_ME, 6, 6);
        _room.AddFloorItem(BLOCK, 3, 3);
        _room.AddWallItem(WALL_ITEM);
        _room.AddBox<WiredTriggerClickFurni>(TRIGGER, 7, 7, "wf_trg_click_furni");

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
        (
            await _room.SaveAsync<UpdateActionMessage>(
                ACTION,
                intParams: intParams,
                stuffIds: [-WALL_ITEM],
                stuffIds2: stuffIds2,
                furniSources: stuffIds2 is null
                    ?
                    [
                        [WiredFurniSourceType.SelectedItems],
                    ]
                    :
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
                StackIds = [_room.Map.ToIdx(7, 7)],
            },
            Ct
        );
        await TickAsync(1);
    }

    private async Task ClickAsync()
    {
        await _room
            .FloorItem(CLICK_ME)
            .Logic.OnClickAsync(ActionContext.CreateForPlayer((PlayerId)105, (RoomId)1), 0, Ct);
        await TickAsync(4);
    }

    private async Task TickAsync(int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            _now += 1_000;
            await Wired.ProcessWiredAsync(_now, dormant: false, Ct);
        }
    }
}
