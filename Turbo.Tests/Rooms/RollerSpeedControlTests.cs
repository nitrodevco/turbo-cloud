using System.Collections;
using FluentAssertions;
using Turbo.Logging;
using Turbo.Primitives;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Snapshots.StuffData;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Events.RoomItem;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The Roller Speed Controller (<c>conf_queue_speed</c>), plain furniture before (no logic), as
/// Habbo runs it (official client capture, 2026-10-18): placed at "0", rollers every 500 ms,
/// each use the next speed (0, 3, 6, 9: 500, 1000, 1500, 2000 ms) and every move shows its
/// other two states in turn in the move's room message; without one, rollers every 2000 ms.
/// </summary>
public sealed class RollerSpeedControlTests
{
    private const int ROLLER = 41;
    private const int CRATE = 45;
    private const int CONTROLLER = 46;

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ActionContext Owner => ActionContext.CreateForSystem(1);

    private RoomRollerSystem Rollers => _room.Harness.Module<RoomRollerSystem>();

    public RollerSpeedControlTests()
    {
        // Four rollers in a loop with a crate on them, so every move slides it.
        AddRoller(ROLLER, 1, 1, Rotation.East);
        AddRoller(ROLLER + 1, 2, 1, Rotation.South);
        AddRoller(ROLLER + 2, 2, 2, Rotation.West);
        AddRoller(ROLLER + 3, 1, 2, Rotation.North);
        _room.AddFloorItem(CRATE, 1, 1).SetPositionZ(Altitude.FromInt(100));
        RoomHarness.SetMember(_room.Harness.State, "IsFurniLoaded", true);
    }

    [Fact]
    public async Task Without_a_controller_rollers_move_every_2000_ms()
    {
        await RescanAsync();

        (await MovesOverAsync(4_000)).Should().Equal(10_000, 12_000, 14_000);
    }

    [Fact]
    public async Task A_placed_controller_starts_at_0_moves_rollers_every_500_ms_and_flashes_with_each_move()
    {
        var controller = await PlaceControllerAsync(state: "6");

        controller.Logic.GetState().Should().Be(0, "a placed controller starts at its first speed");
        (await MovesOverAsync(1_500)).Should().Equal(10_000, 10_500, 11_000, 11_500);
        ControllerStates().Should().Equal(1, 2, 1, 2);

        // The controller's state rides in the move's own room message, after the slide.
        var lastMove = Outbound().Last();
        lastMove.Composers[0].Should().BeOfType<SlideObjectBundleMessageComposer>();
        lastMove.Composers[^1].Should().BeOfType<ObjectDataUpdateMessageComposer>();
    }

    [Fact]
    public async Task Each_use_goes_to_the_next_speed_and_wraps_after_four()
    {
        var controller = await PlaceControllerAsync();
        var states = new List<int>();

        for (var i = 0; i < 4; i++)
        {
            await controller.Logic.OnUseAsync(Owner, 0, Ct);
            states.Add(controller.Logic.GetState());
        }

        states.Should().Equal(3, 6, 9, 0);

        await controller.Logic.OnUseAsync(Owner, 0, Ct);

        (await MovesOverAsync(3_000)).Should().Equal(10_000, 11_000, 12_000, 13_000);
        ControllerStates().Should().Equal(3, 6, 9, 0, 3, 4, 5, 4, 5);
    }

    [Fact]
    public async Task Picking_the_controller_up_puts_the_rollers_back_on_2000_ms_at_once()
    {
        var controller = await PlaceControllerAsync();
        await controller.Logic.OnUseAsync(Owner, 0, Ct);
        (await MovesOverAsync(1_000)).Should().Equal(10_000, 11_000);

        ((IDictionary)RoomHarness.GetMember(_room.Harness.State, "ItemsById")!).Remove(
            (RoomObjectId)CONTROLLER
        );
        await controller.Logic.OnDetachAsync(Ct);
        await RescanAsync();

        (await MovesOverAsync(4_000)).Should().Equal(13_000, 15_000);
    }

    [Fact]
    public async Task A_second_controller_is_refused_with_the_type_limit()
    {
        await PlaceControllerAsync();
        var second = _room.AddFloorItem(
            CONTROLLER + 1,
            6,
            6,
            "roller_speed_control",
            createLogic: (factory, ctx) => new FurnitureRollerSpeedControlLogic(factory, ctx)
        );
        await _room.Harness.Module<RoomObjectModule>().RemoveObjectAsync(Owner, second, Ct, 1);

        var place = () =>
            _room
                .Harness.Module<RoomFurniModule>()
                .PlaceFloorItemAsync(Owner, second, 6, 6, Rotation.North, Ct);

        (await place.Should().ThrowAsync<TurboException>())
            .Which.ErrorCode.Should()
            .Be(TurboErrorCodeEnum.FurniTypeLimitReached);
        _room
            .Harness.Module<RoomFurniModule>()
            .TryGetItem(CONTROLLER + 1, out _)
            .Should()
            .BeFalse();
    }

    [Fact]
    public async Task A_player_without_rights_cannot_change_the_speed()
    {
        var controller = await PlaceControllerAsync();

        await controller.Logic.OnUseAsync(
            ActionContext.CreateForPlayer((PlayerId)105, (RoomId)1),
            0,
            Ct
        );

        controller.Logic.GetState().Should().Be(0);
    }

    private async Task<IRoomFloorItem> PlaceControllerAsync(string state = "0")
    {
        var controller = _room.AddFloorItem(
            CONTROLLER,
            5,
            5,
            "roller_speed_control",
            createLogic: (factory, ctx) => new FurnitureRollerSpeedControlLogic(factory, ctx)
        );

        controller.Logic.StuffData.SetState(state);
        await controller.Logic.OnPlaceAsync(Owner, Ct);
        await controller.Logic.OnAttachAsync(Ct);
        await RescanAsync();

        return controller;
    }

    private void AddRoller(int id, int x, int y, Rotation rotation) =>
        _room
            .AddFloorItem(
                id,
                x,
                y,
                "roller",
                createLogic: (factory, ctx) => new FurnitureRollerLogic(factory, ctx)
            )
            .SetRotation(rotation);

    private Task RescanAsync() =>
        Rollers.OnRoomEventAsync(
            new RoomRollerChangedEvent
            {
                RoomId = 1,
                CausedBy = Owner,
                ObjectId = ROLLER,
            },
            Ct
        );

    // Runs the room's 50 ms ticks for a while and gives the times the rollers moved at.
    private async Task<List<long>> MovesOverAsync(long ms)
    {
        var moves = new List<long>();
        var end = _now + ms;

        for (; _now <= end; _now += 50)
        {
            var before = Outbound().Count();

            await Rollers.ProcessRollersAsync(_now, Ct);

            if (
                Outbound()
                    .Skip(before)
                    .Any(x => x.Composers.Any(c => c is SlideObjectBundleMessageComposer))
            )
                moves.Add(_now);
        }

        return moves;
    }

    private IEnumerable<int> ControllerStates() =>
        Outbound()
            .SelectMany(x => x.Composers)
            .OfType<ObjectDataUpdateMessageComposer>()
            .Where(x => x.ObjectId == CONTROLLER)
            .Select(x => int.Parse(((LegacyStuffSnapshot)x.StuffData).Data));

    private IEnumerable<RoomOutboundSnapshot> Outbound() =>
        _room.Harness.Fakes.Log.Calls.SelectMany(x => x.Args).OfType<RoomOutboundSnapshot>();
}
