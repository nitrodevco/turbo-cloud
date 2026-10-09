using System.Collections;
using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Messages.Outgoing.Room.Furniture;
using Turbo.Primitives.Messages.Outgoing.Room.Session;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Object.Logic.Furniture.Floor;
using Turbo.Rooms.Wired.Variables.Furniture;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The Invisible Furni Controller and the Room Area Hider (tester report 2026-10-09: the
/// controller "doesn't work correctly", the hider "doesn't work at all"). Their definitions had
/// no logic (<c>MapConfigurationFurniLogic</c>); the controller had none to have, and the hider
/// never told a player walking in, nor hid what stood in its area.
/// </summary>
public sealed class RoomConfigurationFurniTests
{
    private const int CONTROLLER = 30;
    private const int HIDER = 31;
    private const int INSIDE = 32;
    private const int OUTSIDE = 33;

    private readonly WiredRoom _room = new(8, 8);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ActionContext Owner => ActionContext.CreateForSystem(1);

    [Fact]
    public async Task Switching_the_controller_on_hides_invisible_layers_for_everyone_and_for_newcomers()
    {
        var controller = _room.AddFloorItem(
            CONTROLLER,
            1,
            1,
            "invisible_furni_control",
            createLogic: (factory, ctx) => new FurnitureInvisibleFurniControlLogic(factory, ctx)
        );

        (await EntryViewAsync()).InvisibleFurni.Should().BeFalse();

        await controller.Logic.OnUseAsync(Owner, 0, Ct);

        SentToRoom<ConfigurationItemStatesMessageComposer>()
            .Should()
            .ContainSingle()
            .Which.InvisibleFurni.Should()
            .BeTrue();
        (await EntryViewAsync()).InvisibleFurni.Should().BeTrue();

        await controller.Logic.OnUseAsync(Owner, 0, Ct);

        SentToRoom<ConfigurationItemStatesMessageComposer>()
            .Last()
            .InvisibleFurni.Should()
            .BeFalse();
    }

    [Fact]
    public async Task The_hider_hides_its_area_and_what_stands_in_it_and_shows_it_again()
    {
        var hider = _room.AddFloorItem(
            HIDER,
            7,
            7,
            "area_hide",
            createLogic: (factory, ctx) => new FurnitureAreaHideLogic(factory, ctx)
        );
        _room.AddFloorItem(INSIDE, 2, 2);
        _room.AddFloorItem(OUTSIDE, 5, 5);

        (
            await hider.Logic.OnInteractAsync(
                Owner,
                new SetAreaHideInteraction
                {
                    RootX = 1,
                    RootY = 1,
                    Width = 3,
                    Length = 3,
                    Invisibility = false,
                    WallItems = false,
                    Invert = false,
                },
                Ct
            )
        ).Should().BeTrue();
        SentToRoom<ObjectRemoveMessageComposer>().Should().BeEmpty("the hider is still off");

        await hider.Logic.OnUseAsync(Owner, 0, Ct);

        SentToRoom<ObjectRemoveMessageComposer>()
            .Select(x => x.ObjectId)
            .Should()
            .Equal((RoomObjectId)INSIDE);
        var view = await EntryViewAsync();
        view.AreaHides.Should().ContainSingle().Which.On.Should().BeTrue();
        view.FloorItems.Select(x => x.ObjectId)
            .Should()
            .BeEquivalentTo([(RoomObjectId)HIDER, (RoomObjectId)OUTSIDE]);

        await hider.Logic.OnUseAsync(Owner, 0, Ct);

        SentToRoom<ObjectAddMessageComposer>()
            .Select(x => x.FloorItem.ObjectId)
            .Should()
            .Equal((RoomObjectId)INSIDE);
        SentToRoom<AreaHideMessageComposer>().Last().AreaHideData.On.Should().BeFalse();
        (await EntryViewAsync()).AreaHides.Should().BeEmpty();
    }

    [Fact]
    public async Task A_furni_wired_moves_into_a_hidden_area_is_hidden_and_shown_again_when_moved_out()
    {
        var hider = _room.AddFloorItem(
            HIDER,
            7,
            7,
            "area_hide",
            createLogic: (factory, ctx) => new FurnitureAreaHideLogic(factory, ctx)
        );
        _room.AddFloorItem(INSIDE, 2, 2);

        await hider.Logic.OnInteractAsync(
            Owner,
            new SetAreaHideInteraction
            {
                RootX = 4,
                RootY = 1,
                Width = 3,
                Length = 3,
                Invisibility = false,
                WallItems = false,
                Invert = false,
            },
            Ct
        );
        await hider.Logic.OnUseAsync(Owner, 0, Ct);

        await WiredMoveXAsync(INSIDE, 5);

        WiredMoves().Should().BeEmpty("the furni went out of sight");
        SentToRoom<ObjectRemoveMessageComposer>()
            .Select(x => x.ObjectId)
            .Should()
            .Equal((RoomObjectId)INSIDE);

        await WiredMoveXAsync(INSIDE, 6);

        WiredMoves().Should().BeEmpty("it stays out of sight");

        await WiredMoveXAsync(INSIDE, 2);

        WiredMoves().Should().BeEmpty("it is sent again instead");
        SentToRoom<ObjectAddMessageComposer>()
            .Select(x => x.FloorItem.ObjectId)
            .Should()
            .Equal((RoomObjectId)INSIDE);

        await WiredMoveXAsync(INSIDE, 1);

        WiredMoves().Should().ContainSingle(x => x.ObjectId == INSIDE && x.TargetX == 1);
    }

    // Moves a furni as wired does (the Creator Tools position write), flushing one action.
    private async Task WiredMoveXAsync(int id, int x)
    {
        var variable = new FurniturePositionXVariable(_room.Harness.Room);
        var varId = variable.GetVarSnapshot().VariableId;

        ((IDictionary)RoomHarness.GetMember(_room.Harness.Room.WiredSystem, "_variableById")!)[
            varId
        ] = variable;

        (
            await _room.Harness.Room.WiredSystem.ApplyVariableMenuOperationAsync(
                new WiredVariableBinding(WiredVariableTargetType.Furni, id),
                varId,
                WiredVariableMenuOperationType.SetValue,
                x,
                Ct
            )
        )
            .Should()
            .BeTrue();
    }

    private List<Turbo.Primitives.Rooms.Snapshots.Wired.WiredFloorItemMovementSnapshot> WiredMoves() =>
        [.. SentToRoom<WiredMovementsMessageComposer>().SelectMany(x => x.FloorItems)];

    private Task<RoomEntryViewSnapshot> EntryViewAsync() =>
        _room.Harness.Room.GetEntryViewAsync((PlayerId)105, Ct);

    private IEnumerable<T> SentToRoom<T>() =>
        _room
            .Harness.Fakes.Log.Calls.SelectMany(x => x.Args)
            .OfType<RoomOutboundSnapshot>()
            .SelectMany(x => x.Composers)
            .OfType<T>();
}
