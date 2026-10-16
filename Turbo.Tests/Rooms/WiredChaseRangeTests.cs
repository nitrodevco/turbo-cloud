using FluentAssertions;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Wired;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Chase as Habbo runs it (official client capture 2026-10-16, Periodically + Chase, a lamp and
/// one user): the lamp chased from 7, 6, 5, 4, 3 and 2 tiles, one orthogonal tile per firing, and
/// never moved once adjacent. Ours only noticed a user 3 tiles away.
/// </summary>
public sealed class WiredChaseRangeTests
{
    private const int BOX = 7;
    private const int LAMP = 30;

    private readonly WiredRoom _room = new(10, 4);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_user_seven_tiles_away_is_chased_one_tile_per_firing()
    {
        _room.Enter(5, 1, 1);
        var box = await PlaceAsync(lampX: 8);

        await box.ExecuteAsync(
            new WiredExecutionContext(_room.Harness.Room) { CancellationToken = Ct },
            Ct
        );

        (_room.FloorItem(LAMP).X, _room.FloorItem(LAMP).Y).Should().Be((7, 1));
    }

    [Fact]
    public async Task An_adjacent_user_is_not_moved_onto()
    {
        _room.Enter(5, 1, 1);
        var box = await PlaceAsync(lampX: 2);

        await box.ExecuteAsync(
            new WiredExecutionContext(_room.Harness.Room) { CancellationToken = Ct },
            Ct
        );

        (_room.FloorItem(LAMP).X, _room.FloorItem(LAMP).Y).Should().Be((2, 1));
    }

    private async Task<WiredActionChaseHabbo> PlaceAsync(int lampX)
    {
        _room.AddFloorItem(LAMP, lampX, 1);

        var box = _room.AddBox<WiredActionChaseHabbo>(BOX, 0, 3, "wf_act_chase");

        (
            await _room.SaveAsync<UpdateActionMessage>(
                BOX,
                stuffIds: [LAMP],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();

        return box;
    }
}
