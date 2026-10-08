using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Selectors;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The Wired Faculty tutorial "Making counters with number blocks" (08/03/2025), counting
/// forward. Three number blocks (states 0-9) stand in a row: hundreds at (1,3), tens at (2,3),
/// ones at (3,3). Stack one toggles the ones on each click. Stack two: "Furni State Is Changed"
/// on every block, for the current state, saved while they show 0; "Furni In Neighborhood" from
/// the triggering item, the tile to its left; "Furni By Type" filtering that to number blocks;
/// "Toggle Furni State" on the selector's furni. A block turning to 0 carries one to its left.
/// </summary>
public sealed class WiredNumberBlockCounterTutorialTests
{
    private const int CLICK_ME = 23;
    private const int HUNDREDS = 30;
    private const int TENS = 31;
    private const int ONES = 32;
    private const int NUMBER_BLOCK = 900;

    /// <summary>The neighbourhood drawing with only the tile left of the centre: spiral rank 5.</summary>
    private static readonly int[] LEFT_TILE = [1 << 5, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    [Theory]
    [InlineData(9, 0, 0, 9)]
    [InlineData(10, 0, 1, 0)]
    [InlineData(27, 0, 2, 7)]
    [InlineData(100, 1, 0, 0)]
    public async Task Clicks_count_up_across_the_blocks(
        int clicks,
        int hundreds,
        int tens,
        int ones
    )
    {
        await BuildAsync();

        for (var i = 0; i < clicks; i++)
        {
            await _room
                .FloorItem(CLICK_ME)
                .Logic.OnClickAsync(ActionContext.CreateForPlayer((PlayerId)105, (RoomId)1), 0, Ct);
            // A wired tick per carry: the click, the ones, the tens.
            await TickAsync(5);
        }

        (State(HUNDREDS), State(TENS), State(ONES)).Should().Be((hundreds, tens, ones));
    }

    private int State(int id) => _room.FloorItem(id).Logic.GetState();

    private async Task BuildAsync()
    {
        _room.Enter(5, 7, 7);
        _room.AddFloorItem(CLICK_ME, 7, 4);

        foreach (var (id, x) in new[] { (HUNDREDS, 1), (TENS, 2), (ONES, 3) })
        {
            var block = _room.AddFloorItem(id, x, 3, "number_block", definitionId: NUMBER_BLOCK);

            block
                .GetType()
                .GetProperty(nameof(IRoomFloorItem.Definition))!
                .SetValue(block, block.Definition with { TotalStates = 10 });
        }

        _room.AddBox<WiredTriggerClickFurni>(1, 0, 0, "wf_trg_click_furni");
        _room.AddBox<WiredActionToggleItemState>(2, 0, 0, "wf_act_toggle_state");
        _room.AddBox<WiredTriggerItemStateUpdated>(3, 0, 5, "wf_trg_state_changed");
        _room.AddBox<WiredSelectorItemsInNeighborhood>(4, 0, 5, "wf_slc_furni_neighborhood");
        _room.AddBox<WiredSelectorItemsByType>(5, 0, 5, "wf_slc_furni_bytype");
        _room.AddBox<WiredActionToggleItemState>(6, 0, 5, "wf_act_toggle_state");

        (
            await _room.SaveAsync<UpdateTriggerMessage>(
                1,
                stuffIds: [CLICK_ME],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ]
            )
        ).Should().BeTrue();
        (
            await _room.SaveAsync<UpdateActionMessage>(
                2,
                intParams: [0],
                stuffIds: [ONES],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();
        (
            await _room.SaveAsync<UpdateTriggerMessage>(
                3,
                intParams: [1],
                stuffIds: [HUNDREDS, TENS, ONES],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ]
            )
        ).Should().BeTrue();
        (
            await _room.SaveAsync<UpdateSelectorMessage>(
                4,
                intParams: [0, 0, 0, .. LEFT_TILE],
                furniSources:
                [
                    [WiredFurniSourceType.TriggeredItem],
                ],
                definitionSpecifics: [false, false]
            )
        ).Should().BeTrue();
        (
            await _room.SaveAsync<UpdateSelectorMessage>(
                5,
                intParams: [0],
                stuffIds: [ONES],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ],
                definitionSpecifics: [true, false]
            )
        ).Should().BeTrue();
        (
            await _room.SaveAsync<UpdateActionMessage>(
                6,
                intParams: [0],
                furniSources:
                [
                    [WiredFurniSourceType.SelectorItems],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();

        foreach (var y in new[] { 0, 5 })
            await Wired.OnRoomEventAsync(
                new RoomWiredStackChangedEvent
                {
                    RoomId = 1,
                    CausedBy = ActionContext.CreateForSystem(1),
                    StackIds = [_room.Map.ToIdx(0, y)],
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
