using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Messages.Outgoing.Room.Furniture;
using Turbo.Primitives.Messages.Outgoing.Room.Session;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
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
    private const int INSIDE_TOO = 34;
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
        // Official client capture 2026-10-22: on = ObjectDataUpdate, one ObjectRemoveMultiple
        // {ids, picker -1, byInvisibleFurniManager} of the area's furni, AreaHide, then the area's
        // tiles -1 in a HeightMapUpdate; off = ObjectDataUpdate, one Objects, AreaHide, heights.
        var hider = _room.AddFloorItem(
            HIDER,
            7,
            7,
            "area_hide",
            createLogic: (factory, ctx) => new FurnitureAreaHideLogic(factory, ctx)
        );
        _room.AddFloorItem(INSIDE, 2, 2);
        _room.AddFloorItem(INSIDE_TOO, 3, 3);
        _room.AddFloorItem(OUTSIDE, 5, 5);

        (await SetAreaAsync(hider, invisible: false)).Should().BeTrue();
        AreaHideSends()
            .Should()
            .Equal(["ObjectDataUpdate"], "a save while it is off is the data alone");

        await hider.Logic.OnUseAsync(Owner, 0, Ct);

        AreaHideSends().Should().Equal(["ObjectDataUpdate", "ObjectRemoveMultiple", "AreaHide"]);
        _room
            .Harness.Fakes.SentToRoom<ObjectRemoveMultipleMessageComposer>()
            .Should()
            .ContainSingle()
            .Which.Should()
            .BeEquivalentTo(
                new
                {
                    ObjectIdsToRemove = new long[] { INSIDE, INSIDE_TOO },
                    PickerId = PlayerId.Invalid,
                    ByInvisibleFurniManager = true,
                }
            );
        (await EncodedHeightAsync(2, 2)).Should().Be(-1, "the area is no floor while hidden");
        (await EncodedHeightAsync(5, 5)).Should().NotBe(-1);
        var view = await EntryViewAsync();
        view.AreaHides.Should().ContainSingle().Which.On.Should().BeTrue();
        view.FloorItems.Select(x => x.ObjectId)
            .Should()
            .BeEquivalentTo([(RoomObjectId)HIDER, (RoomObjectId)OUTSIDE]);

        await hider.Logic.OnUseAsync(Owner, 0, Ct);

        AreaHideSends().Should().Equal(["ObjectDataUpdate", "Objects", "AreaHide"]);
        _room
            .Harness.Fakes.SentToRoom<ObjectsMessageComposer>()
            .Should()
            .ContainSingle()
            .Which.FloorItems.Select(x => x.ObjectId)
            .Should()
            .BeEquivalentTo([(RoomObjectId)INSIDE, (RoomObjectId)INSIDE_TOO]);
        _room
            .Harness.Fakes.SentToRoom<AreaHideMessageComposer>()
            .Last()
            .AreaHideData.On.Should()
            .BeFalse();
        (await EncodedHeightAsync(2, 2)).Should().NotBe(-1);
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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task The_controller_takes_away_the_invisible_furni_and_brings_them_back(
        bool invisible
    )
    {
        // Tester report 2026-10-22: "Make this furniture invisible" ticked, and the controller
        // did nothing to the hider. Official client capture 2026-10-22: on = ObjectDataUpdate,
        // ConfigurationItemStates, one ObjectRemoveMultiple of the hider set invisible and the
        // room_invisible_block; off = the same with one Objects bringing them back.
        var controller = _room.AddFloorItem(
            CONTROLLER,
            1,
            1,
            "invisible_furni_control",
            createLogic: (factory, ctx) => new FurnitureInvisibleFurniControlLogic(factory, ctx)
        );
        var hider = _room.AddFloorItem(
            HIDER,
            7,
            7,
            "area_hide",
            createLogic: (factory, ctx) => new FurnitureAreaHideLogic(factory, ctx)
        );
        _room.AddFloorItem(INSIDE, 6, 6, className: "room_invisible_block");
        (await SetAreaAsync(hider, invisible)).Should().BeTrue();
        AreaHideSends().Should().Equal(["ObjectDataUpdate"], "the option alone hides nothing");

        await controller.Logic.OnUseAsync(Owner, 0, Ct);

        RoomObjectId[] gone = invisible ? [HIDER, INSIDE] : [INSIDE];
        AreaHideSends()
            .Should()
            .Equal(["ObjectDataUpdate", "ConfigurationItemStates", "ObjectRemoveMultiple"]);
        _room
            .Harness.Fakes.SentToRoom<ObjectRemoveMultipleMessageComposer>()
            .Should()
            .ContainSingle()
            .Which.Should()
            .BeEquivalentTo(
                new
                {
                    ObjectIdsToRemove = gone.Select(x => (long)x.Value).ToArray(),
                    PickerId = PlayerId.Invalid,
                    ByInvisibleFurniManager = true,
                }
            );
        (await EntryViewAsync()).FloorItems.Select(x => x.ObjectId).Should().NotContain(gone);

        await controller.Logic.OnUseAsync(Owner, 0, Ct);

        AreaHideSends().Should().Equal(["ObjectDataUpdate", "ConfigurationItemStates", "Objects"]);
        _room
            .Harness.Fakes.SentToRoom<ObjectsMessageComposer>()
            .Should()
            .ContainSingle()
            .Which.FloorItems.Select(x => x.ObjectId)
            .Should()
            .BeEquivalentTo(gone);
        (await EntryViewAsync()).FloorItems.Select(x => x.ObjectId).Should().Contain(gone);
    }

    [Fact]
    public async Task A_hider_made_invisible_while_the_controller_is_on_goes_at_once()
    {
        // Official client capture 2026-10-22 (save while on): ObjectDataUpdate, then
        // ObjectRemoveMultiple [hider], no AreaHide.
        var controller = _room.AddFloorItem(
            CONTROLLER,
            1,
            1,
            "invisible_furni_control",
            createLogic: (factory, ctx) => new FurnitureInvisibleFurniControlLogic(factory, ctx)
        );
        var hider = _room.AddFloorItem(
            HIDER,
            7,
            7,
            "area_hide",
            createLogic: (factory, ctx) => new FurnitureAreaHideLogic(factory, ctx)
        );
        await SetAreaAsync(hider, invisible: false);
        await hider.Logic.OnUseAsync(Owner, 0, Ct);
        await controller.Logic.OnUseAsync(Owner, 0, Ct);
        AreaHideSends();

        (await SetAreaAsync(hider, invisible: true)).Should().BeTrue();

        AreaHideSends().Should().Equal(["ObjectDataUpdate", "ObjectRemoveMultiple"]);
        _room
            .Harness.Fakes.SentToRoom<ObjectRemoveMultipleMessageComposer>()
            .Last()
            .ObjectIdsToRemove.Should()
            .Equal(HIDER);
        var view = await EntryViewAsync();
        view.FloorItems.Select(x => x.ObjectId).Should().NotContain((RoomObjectId)HIDER);
        view.AreaHides.Should().ContainSingle("its area stays hidden");
    }

    [Theory]
    [InlineData("room_invisible_block")]
    [InlineData("room_invisible_block2")]
    [InlineData("room_invisible_block3")]
    [InlineData("room_invisible_block4")]
    [InlineData("room_invisible_sit_tile")]
    [InlineData("room_invisible_click_tile")]
    [InlineData("room_invisible_lay_tile")]
    public async Task The_controller_takes_away_every_invisible_block_and_tile(string className)
    {
        // Official client capture 2026-10-22T23-01-23-UseFurniture (2/2): the controller's one
        // ObjectRemoveMultiple holds all seven invisible classes.
        var controller = _room.AddFloorItem(
            CONTROLLER,
            1,
            1,
            "invisible_furni_control",
            createLogic: (factory, ctx) => new FurnitureInvisibleFurniControlLogic(factory, ctx)
        );
        _room.AddFloorItem(INSIDE, 6, 6, className: className);

        await controller.Logic.OnUseAsync(Owner, 0, Ct);

        _room
            .Harness.Fakes.SentToRoom<ObjectRemoveMultipleMessageComposer>()
            .Should()
            .ContainSingle()
            .Which.ObjectIdsToRemove.Should()
            .Equal(INSIDE);
    }

    [Fact]
    public async Task A_furni_moved_across_a_hidden_areas_edge_is_removed_or_added_then_updated()
    {
        // Official client capture 2026-10-22T23-04-14-MoveObject: into the area = ObjectRemove
        // {picker -1, byInvisibleFurniManager} then ObjectUpdate (2/2); out of it = ObjectAdd
        // then ObjectUpdate (3/3).
        var hider = _room.AddFloorItem(
            HIDER,
            7,
            7,
            "area_hide",
            createLogic: (factory, ctx) => new FurnitureAreaHideLogic(factory, ctx)
        );
        _room.AddFloorItem(OUTSIDE, 5, 5);
        await SetAreaAsync(hider, invisible: false);
        await hider.Logic.OnUseAsync(Owner, 0, Ct);
        var furni = _room.Harness.Module<RoomFurniModule>();

        (await furni.MoveFloorItemByIdAsync(Owner, OUTSIDE, 2, 2, null, null, Ct))
            .Should()
            .BeTrue();

        MoveSends().Should().Equal(["ObjectRemove", "ObjectUpdate"]);

        (await furni.MoveFloorItemByIdAsync(Owner, OUTSIDE, 5, 5, null, null, Ct))
            .Should()
            .BeTrue();

        MoveSends().Should().Equal(["ObjectAdd", "ObjectUpdate"]);

        (await furni.MoveFloorItemByIdAsync(Owner, OUTSIDE, 6, 5, null, null, Ct))
            .Should()
            .BeTrue();

        MoveSends().Should().Equal(["ObjectUpdate"], "a move in sight is the update alone");
    }

    // The single-furni packets the room got since the last call, by Habbo's message names.
    private string[] MoveSends()
    {
        var all = _room.Harness.Fakes.Sent(SendRoute.Room).ToList();
        var sent = all.Skip(_moveSeen)
            .Select(x => x.GetType().Name.Replace("MessageComposer", ""))
            .Where(x => x is "ObjectRemove" or "ObjectAdd" or "ObjectUpdate")
            .ToArray();

        _moveSeen = all.Count;

        return sent;
    }

    private int _moveSeen;

    private Task<bool> SetAreaAsync(IRoomFloorItem hider, bool invisible) =>
        hider.Logic.OnInteractAsync(
            Owner,
            new SetAreaHideInteraction
            {
                RootX = 1,
                RootY = 1,
                Width = 3,
                Length = 3,
                Invisibility = invisible,
                WallItems = false,
                Invert = false,
            },
            Ct
        );

    // The furni and area packets the room got since the last call, by Habbo's message names.
    private string[] AreaHideSends()
    {
        var all = _room.Harness.Fakes.Sent(SendRoute.Room).ToList();
        var sent = all.Skip(_seen)
            .Select(x => x.GetType().Name.Replace("MessageComposer", ""))
            .Where(x =>
                x
                    is "ObjectDataUpdate"
                        or "ObjectRemove"
                        or "ObjectRemoveMultiple"
                        or "ObjectAdd"
                        or "Objects"
                        or "AreaHide"
                        or "ConfigurationItemStates"
            )
            .ToArray();

        _seen = all.Count;

        return sent;
    }

    private int _seen;

    private async Task<short> EncodedHeightAsync(int x, int y) =>
        (await _room.Harness.Room.GetMapSnapshotAsync(Ct)).TileEncodedHeights[y * 8 + x];

    private Task<RoomEntryViewSnapshot> EntryViewAsync() =>
        _room.Harness.Room.GetEntryViewAsync((PlayerId)105, Ct);

    /// <summary>What a player walking in is told (<c>RoomGrain.BuildEntryTailAsync</c>).</summary>
    private ConfigurationItemStatesMessageComposer NewcomerStates() =>
        FurnitureConfigurationControlLogic.GetConfigurationItemStates(
            _room.Harness.Module<RoomFurniModule>()
        );
}
