using FluentAssertions;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Wired;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// "Furni to furni" saved the way the client's editor saves it in dual picking mode: the moving
/// furni go in the first pick list and the target in the second ("secondary furni picks", source
/// 101). Both lists reach the box, so the furni move onto the target.
/// </summary>
public sealed class WiredSecondaryPicksTests
{
    private const int BOX = 7;
    private const int MOVER = 10;
    private const int TARGET = 11;

    private readonly WiredRoom _room = new(8, 8);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void TheSecondarySourceIs101OnTheWire_AndSnapshotIs110()
    {
        WiredFurniSourceTypeExtensions
            .GetProtocolId(WiredFurniSourceType.SecondaryItems)
            .Should()
            .Be(WiredSourceType.SecondaryItems);
        ((int)WiredSourceType.SecondaryItems).Should().Be(101);
        ((int)WiredSourceType.SnapshotItems).Should().Be(110);
        WiredFurniSourceTypeExtensions
            .FromProtocolId((WiredSourceType)101)
            .Should()
            .Be(WiredFurniSourceType.SecondaryItems);
    }

    [Fact]
    public async Task FurniToFurni_MovesThePickedFurniOntoTheSecondaryPick()
    {
        _room.AddFloorItem(MOVER, 1, 1);
        _room.AddFloorItem(TARGET, 5, 5);
        var box = _room.AddBox<WiredActionFurniToFurni>(BOX, 0, 0, "wf_act_furni_to_furni");

        (
            await _room.SaveAsync<UpdateActionMessage>(
                BOX,
                stuffIds: [MOVER],
                stuffIds2: [TARGET],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                    [WiredFurniSourceType.SecondaryItems],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();

        box.GetFurniSources()
            .Select(slot => slot.Single())
            .Should()
            .Equal(WiredFurniSourceType.SelectedItems, WiredFurniSourceType.SecondaryItems);
        box.GetStuffIds2().Should().Equal(TARGET);

        var moved = await box.ExecuteAsync(
            new WiredExecutionContext(_room.Harness.Room) { CancellationToken = Ct },
            Ct
        );

        moved.Should().BeTrue();
        _room.FloorItem(MOVER).X.Should().Be(5);
        _room.FloorItem(MOVER).Y.Should().Be(5);
    }

    [Fact]
    public void TheEditorIsOfferedTheSecondaryPicksOnTheBoxesThatTakeTwoFurniSets()
    {
        foreach (
            var allowed in new[]
            {
                _room
                    .AddBox<WiredActionFurniToFurni>(1, 0, 0, "wf_act_furni_to_furni")
                    .GetAllowedFurniSources(),
                _room
                    .AddBox<WiredActionMoveFurniTo>(2, 0, 1, "wf_act_move_furni_to")
                    .GetAllowedFurniSources(),
                _room
                    .AddBox<WiredActionSendSignal>(3, 0, 2, "wf_act_send_signal")
                    .GetAllowedFurniSources(),
            }
        )
        {
            // Dual picking needs the first pick list on one slot and the second on another.
            allowed.SelectMany(slot => slot).Should().Contain(WiredFurniSourceType.SelectedItems);
            allowed[1].Should().Contain(WiredFurniSourceType.SecondaryItems);
        }
    }
}
