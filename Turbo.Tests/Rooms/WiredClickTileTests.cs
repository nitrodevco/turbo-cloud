using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// "User Clicks Tile" picks invisible click tiles and fires for those (tester report 2026-10-09,
/// after a Wired Faculty video where the trigger's picks are the yellow click tiles). It used to
/// pick nothing and fire for a click on any click tile in the room. Anything else is refused
/// with the hotel's own text, <c>wiredfurni.error.require_click_tiles</c>.
/// </summary>
public sealed class WiredClickTileTests
{
    private const int PLAYER = 5;
    private const int PICKED_TILE = 10;
    private const int OTHER_TILE = 11;
    private const int CHAIR = 12;
    private const int TRIGGER = 1;
    private const int GIVE = 2;
    private const int MARK = 20;

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;
    private WiredVariableUser _mark = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    [Fact]
    public async Task Picking_anything_but_a_click_tile_is_refused_with_the_hotels_text()
    {
        await BuildAsync();

        var result = await _room.SaveWithResultAsync<UpdateTriggerMessage>(
            TRIGGER,
            stuffIds: [PICKED_TILE, CHAIR]
        );

        result.IsSaved.Should().BeFalse();
        result.ErrorKey.Should().Be(WiredSaveErrors.REQUIRE_CLICK_TILES);
    }

    [Theory]
    [InlineData(2, 2, true)]
    [InlineData(4, 4, false)]
    public async Task Only_a_click_on_a_picked_click_tile_fires(int x, int y, bool fires)
    {
        await BuildAsync();
        (await _room.SaveAsync<UpdateTriggerMessage>(TRIGGER, stuffIds: [PICKED_TILE]))
            .Should()
            .BeTrue();
        await RebuildAsync(0, 0);

        await _room.Harness.Room.ClickTileAsync(
            ActionContext.CreateForPlayer((PlayerId)(100 + PLAYER), (RoomId)1),
            x,
            y,
            Ct
        );
        await TickAsync(3);

        _mark
            .TryGetValue(
                new WiredVariableKey(
                    _mark.GetVarSnapshot().VariableId,
                    WiredVariableTargetType.User,
                    PLAYER
                ),
                out _
            )
            .Should()
            .Be(fires);
    }

    private async Task BuildAsync()
    {
        _room.Enter(PLAYER, 1, 1);
        _room.AddFloorItem(
            PICKED_TILE,
            2,
            2,
            "room_invisible_click_tile",
            createLogic: (factory, ctx) => new FurnitureInvisibleClickTileLogic(factory, ctx)
        );
        _room.AddFloorItem(
            OTHER_TILE,
            4,
            4,
            "room_invisible_click_tile",
            createLogic: (factory, ctx) => new FurnitureInvisibleClickTileLogic(factory, ctx)
        );
        _room.AddFloorItem(CHAIR, 5, 5);

        _mark = _room.AddBox<WiredVariableUser>(MARK, 6, 6, "wf_var_user");
        (
            await _room.SaveAsync<UpdateVariableMessage>(
                MARK,
                intParams: [(int)WiredAvailabilityType.UserActive, 1],
                stringParam: "mark"
            )
        )
            .Should()
            .BeTrue();
        await _mark.LoadWiredAsync(Ct);
        await RebuildAsync(6, 6);

        _room.AddBox<WiredTriggerClickTile>(TRIGGER, 0, 0, "wf_trg_click_tile");
        _room.AddBox<WiredActionGiveVariable>(GIVE, 0, 0, "wf_act_give_var");
        (
            await _room.SaveAsync<UpdateActionMessage>(
                GIVE,
                intParams: [(int)WiredVariableTargetType.User, 0, 7, 0],
                definitionSpecifics: [0],
                variableIds: [_mark.GetVarSnapshot().VariableId.ToString()]
            )
        )
            .Should()
            .BeTrue();
    }

    private async Task RebuildAsync(int x, int y)
    {
        await Wired.OnRoomEventAsync(
            new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                StackIds = [_room.Map.ToIdx(x, y)],
            },
            Ct
        );
        await TickAsync(1);
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
