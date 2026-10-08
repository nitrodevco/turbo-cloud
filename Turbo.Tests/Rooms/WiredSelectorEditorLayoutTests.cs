using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Selectors;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;
using Turbo.Rooms.Wired;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Selectors saved the way their editors save them (WIN63-202609161723). "In neighbourhood"
/// (<c>InNeighborhood.readIntParamsFromForm</c>) sends the users flag, the root and the whole 21x21
/// drawing packed 32 tiles to an int: 3 + 14 ints. "With variable" (<c>WithVariable</c>) sends the
/// comparison, the reference (0 none, 1 a typed value, 2 a variable), the value (hi, lo) and the
/// reference target.
/// </summary>
public sealed class WiredSelectorEditorLayoutTests
{
    private readonly WiredRoom _room = new(24, 24);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    [Fact]
    public async Task A_neighbourhood_drawn_to_its_edge_is_saved_and_reaches_ten_tiles_out()
    {
        _room.AddFloorItem(30, 5, 5);
        _room.AddFloorItem(31, 12, 5);
        var selector = _room.AddBox<WiredSelectorItemsInNeighborhood>(
            1,
            0,
            0,
            "wf_slc_furni_neighborhood"
        );

        // The editor packs the 441 tiles into 14 ints.
        int[] drawing = [.. Enumerable.Repeat(-1, 14)];

        (
            await _room.SaveAsync<UpdateSelectorMessage>(
                1,
                intParams: [0, 0, 0, .. drawing],
                stuffIds: [30],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ],
                definitionSpecifics: [false, false]
            )
        ).Should().BeTrue();

        var set = await selector.SelectAsync(Context(), Ct);

        set.SelectedFurniIds.Should().Contain(31);
    }

    [Theory]
    [InlineData(1, 2, new[] { 31 })]
    [InlineData(0, 0, new[] { 30, 31, 32 })]
    public async Task With_variable_reads_the_reference_as_its_editor_saves_it(
        int reference,
        int typedValue,
        int[] expected
    )
    {
        foreach (var id in new[] { 30, 31, 32 })
            _room.AddFloorItem(id, id - 29, 5);

        var score = _room.AddBox<WiredVariableFurni>(10, 4, 4, "wf_var_furni");
        var selector = _room.AddBox<WiredSelectorItemsWithVariable>(
            11,
            0,
            0,
            "wf_slc_furni_with_var"
        );

        (
            await _room.SaveAsync<UpdateVariableMessage>(
                10,
                intParams: [(int)WiredAvailabilityType.RoomActive, 1],
                stringParam: "score"
            )
        )
            .Should()
            .BeTrue();
        await Wired.OnRoomEventAsync(
            new WiredVariableBoxChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                BoxIds = [10],
            },
            Ct
        );
        _now += 1_000;
        await Wired.ProcessWiredAsync(_now, dormant: false, Ct);

        var variableId = score.GetVarSnapshot().VariableId;

        foreach (var (id, value) in new[] { (30, 1), (31, 5), (32, 3) })
            (
                await score.GiveValueAsync(
                    new WiredVariableKey(variableId, WiredVariableTargetType.Furni, id),
                    value
                )
            )
                .Should()
                .BeTrue();

        // Greater than the typed value, when there is one.
        (
            await _room.SaveAsync<UpdateSelectorMessage>(
                11,
                intParams:
                [
                    (int)WiredComparisonType.GreaterThan,
                    reference,
                    0,
                    typedValue + 2,
                    (int)WiredVariableTargetType.Furni,
                ],
                variableIds: [variableId.ToString()],
                definitionSpecifics: [false, false]
            )
        )
            .Should()
            .BeTrue();

        var set = await selector.SelectAsync(Context(), Ct);

        set.SelectedFurniIds.Order().Should().Equal(expected);
    }

    [Fact]
    public async Task With_variable_takes_a_variable_reference()
    {
        _room.AddBox<WiredSelectorItemsWithVariable>(11, 0, 0, "wf_slc_furni_with_var");

        (
            await _room.SaveAsync<UpdateSelectorMessage>(
                11,
                intParams:
                [
                    (int)WiredComparisonType.GreaterThan,
                    2,
                    0,
                    0,
                    (int)WiredVariableTargetType.Global,
                ],
                definitionSpecifics: [false, false]
            )
        )
            .Should()
            .BeTrue();
    }

    private WiredProcessingContext Context() =>
        new(_room.Harness.Room)
        {
            Event = new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                StackIds = [],
            },
            Stack = _room.Harness.Fakes.Create<IWiredStack>(),
        };
}
