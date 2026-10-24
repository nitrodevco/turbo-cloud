using FluentAssertions;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Wired;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// W1 action.34.R8: Move Furni To Furni with several destinations draws one at random each
/// firing (official client capture 2026-10-24, W1 lab, 8 firings over 3 destinations). Ours always
/// took the first. W1 action.3.E6: Match To Snapshot stores a four-int save as
/// [state, direction, position, altitude, 0] (2/2); ours kept the fifth at its fresh 1.
/// </summary>
public sealed class WiredFurniToFurniTargetTests
{
    private const int MOVER = 20;

    private readonly WiredRoom _room = new(10, 10);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task One_of_several_destinations_is_drawn_at_random()
    {
        _room.AddFloorItem(MOVER, 1, 1);
        _room.AddFloorItem(30, 3, 7);
        _room.AddFloorItem(31, 5, 7);
        _room.AddFloorItem(32, 7, 7);

        var box = _room.AddBox<WiredActionFurniToFurni>(1, 0, 0, "wf_act_furni_to_furni");

        (
            await _room.SaveAsync<UpdateActionMessage>(
                1,
                stuffIds: [MOVER],
                stuffIds2: [30, 31, 32],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                    [WiredFurniSourceType.SecondaryItems],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();

        var landed = new HashSet<int>();

        for (var i = 0; i < 30; i++)
        {
            _room.Map.MoveFloorItem(_room.FloorItem(MOVER), _room.Map.ToIdx(1, 1));

            await box.ExecuteAsync(
                new WiredExecutionContext(_room.Harness.Room) { CancellationToken = Ct },
                Ct
            );

            landed.Add(_room.FloorItem(MOVER).X);
        }

        landed.Should().BeSubsetOf([3, 5, 7]).And.HaveCountGreaterThan(1);
    }

    [Fact]
    public async Task Match_to_snapshot_stores_a_four_int_save_with_its_fifth_as_0()
    {
        _room.AddFloorItem(MOVER, 1, 1);

        var box = _room.AddBox<WiredActionMatchToSnapshot>(1, 0, 0, "wf_act_match_to_sshot");

        (
            await _room.SaveAsync<UpdateActionMessage>(
                1,
                intParams: [0, 1, 1, 0],
                stuffIds: [MOVER],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();

        box.GetSnapshot().IntParams.Should().Equal(0, 1, 1, 0, 0);
    }

    // W2 action.3.E13 (2/2): a save that sends the fifth int as 1 stores it as 0 too. Ours kept 1.
    [Fact]
    public async Task Match_to_snapshot_stores_a_fifth_int_sent_as_1_as_0()
    {
        _room.AddFloorItem(MOVER, 1, 1);

        var box = _room.AddBox<WiredActionMatchToSnapshot>(1, 0, 0, "wf_act_match_to_sshot");

        (
            await _room.SaveAsync<UpdateActionMessage>(
                1,
                intParams: [0, 1, 1, 1, 1],
                stuffIds: [MOVER],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();

        box.GetSnapshot().IntParams.Should().Equal(0, 1, 1, 1, 0);
    }
}
