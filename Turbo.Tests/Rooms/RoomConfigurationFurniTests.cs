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
using Turbo.Rooms.Grains.Modules;
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
    private const int HAND_ITEM = 5;

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

        NewcomerStates().InvisibleFurni.Should().BeFalse();

        await controller.Logic.OnUseAsync(Owner, 0, Ct);

        _room
            .Harness.Fakes.SentToRoom<ConfigurationItemStatesMessageComposer>()
            .Should()
            .ContainSingle()
            .Which.InvisibleFurni.Should()
            .BeTrue();
        NewcomerStates().InvisibleFurni.Should().BeTrue();

        await controller.Logic.OnUseAsync(Owner, 0, Ct);

        _room
            .Harness.Fakes.SentToRoom<ConfigurationItemStatesMessageComposer>()
            .Last()
            .InvisibleFurni.Should()
            .BeFalse();
    }

    [Fact]
    public async Task A_switched_on_handitem_blocker_tells_the_room_and_stops_hand_items_being_passed()
    {
        var giver = _room.Enter(1, 2, 2);
        _room.Enter(2, 2, 3);
        giver.SetHandItem(HAND_ITEM);
        var blocker = _room.AddFloorItem(
            CONTROLLER,
            5,
            5,
            "handitem_block",
            createLogic: (factory, ctx) => new FurnitureHanditemBlockLogic(factory, ctx)
        );

        await blocker.Logic.OnUseAsync(Owner, 0, Ct);

        _room
            .Harness.Fakes.SentToRoom<ConfigurationItemStatesMessageComposer>()
            .Should()
            .ContainSingle()
            .Which.Should()
            .BeEquivalentTo(
                new
                {
                    IsHanditemControlBlocked = true,
                    ChooserDisabled = false,
                    InvisibleFurni = false,
                }
            );
        NewcomerStates().IsHanditemControlBlocked.Should().BeTrue();
        (await PassHandItemAsync()).Should().BeFalse();
        giver.HandItemId.Should().Be(HAND_ITEM, "the blocker kept it in the giver's hand");

        await blocker.Logic.OnUseAsync(Owner, 0, Ct);

        _room
            .Harness.Fakes.SentToRoom<ConfigurationItemStatesMessageComposer>()
            .Last()
            .IsHanditemControlBlocked.Should()
            .BeFalse();
        (await PassHandItemAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task A_switched_on_chooser_disabler_tells_the_room_and_newcomers_and_its_pickup_clears_it()
    {
        var disabler = _room.AddFloorItem(
            CONTROLLER,
            1,
            1,
            "chooser_disable",
            createLogic: (factory, ctx) => new FurnitureChooserDisableLogic(factory, ctx)
        );

        NewcomerStates().ChooserDisabled.Should().BeFalse();

        await disabler.Logic.OnUseAsync(Owner, 0, Ct);

        _room
            .Harness.Fakes.SentToRoom<ConfigurationItemStatesMessageComposer>()
            .Should()
            .ContainSingle()
            .Which.ChooserDisabled.Should()
            .BeTrue();
        NewcomerStates().ChooserDisabled.Should().BeTrue();

        await disabler.Logic.OnDetachAsync(Ct);

        _room
            .Harness.Fakes.SentToRoom<ConfigurationItemStatesMessageComposer>()
            .Last()
            .ChooserDisabled.Should()
            .BeFalse("the switched-on disabler is the one leaving");
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
        _room
            .Harness.Fakes.SentToRoom<ObjectRemoveMessageComposer>()
            .Should()
            .BeEmpty("the hider is still off");

        await hider.Logic.OnUseAsync(Owner, 0, Ct);

        _room
            .Harness.Fakes.SentToRoom<ObjectRemoveMessageComposer>()
            .Select(x => x.ObjectId)
            .Should()
            .Equal((RoomObjectId)INSIDE);
        var view = await EntryViewAsync();
        view.AreaHides.Should().ContainSingle().Which.On.Should().BeTrue();
        view.FloorItems.Select(x => x.ObjectId)
            .Should()
            .BeEquivalentTo([(RoomObjectId)HIDER, (RoomObjectId)OUTSIDE]);

        await hider.Logic.OnUseAsync(Owner, 0, Ct);

        _room
            .Harness.Fakes.SentToRoom<ObjectAddMessageComposer>()
            .Select(x => x.FloorItem.ObjectId)
            .Should()
            .Equal((RoomObjectId)INSIDE);
        _room
            .Harness.Fakes.SentToRoom<AreaHideMessageComposer>()
            .Last()
            .AreaHideData.On.Should()
            .BeFalse();
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
        _room
            .Harness.Fakes.SentToRoom<ObjectRemoveMessageComposer>()
            .Select(x => x.ObjectId)
            .Should()
            .Equal((RoomObjectId)INSIDE);

        await WiredMoveXAsync(INSIDE, 6);

        WiredMoves().Should().BeEmpty("it stays out of sight");

        await WiredMoveXAsync(INSIDE, 2);

        WiredMoves().Should().BeEmpty("it is sent again instead");
        _room
            .Harness.Fakes.SentToRoom<ObjectAddMessageComposer>()
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

        _room.Harness.Room.WiredSystem.VariablesById()[varId] = variable;

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
        [
            .. _room
                .Harness.Fakes.SentToRoom<WiredMovementsMessageComposer>()
                .SelectMany(x => x.FloorItems),
        ];

    // Player 101 (object 1) hands what they hold to player 102 (object 2), standing beside them.
    private Task<bool> PassHandItemAsync() =>
        _room.Harness.Room.PassHandItemAsync(ActionContext.CreateForPlayer(101, 1), 102, Ct);

    private Task<RoomEntryViewSnapshot> EntryViewAsync() =>
        _room.Harness.Room.GetEntryViewAsync((PlayerId)105, Ct);

    /// <summary>What a player walking in is told (<c>RoomGrain.BuildEntryTailAsync</c>).</summary>
    private ConfigurationItemStatesMessageComposer NewcomerStates() =>
        FurnitureConfigurationControlLogic.GetConfigurationItemStates(
            _room.Harness.Module<RoomFurniModule>()
        );
}
