using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.RoomItem;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// "Furni state changes" with its two options (<c>wiredfurni.params.state_trigger.0</c> / <c>.1</c>):
/// every change, or only a change to the state the furni had when the box was saved. The picked
/// lamp is in state 0 when the box is saved.
/// </summary>
public sealed class WiredStateChangeTriggerTests
{
    private const int LAMP = 20;

    private readonly WiredRoom _room = new(8, 8);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(false, 1, true)]
    [InlineData(false, 0, true)]
    [InlineData(true, 1, false)]
    [InlineData(true, 0, true)]
    public async Task The_trigger_matches_a_change_to(
        bool currentStateOnly,
        int newState,
        bool fires
    )
    {
        var lamp = _room.AddFloorItem(LAMP, 3, 3);
        var trigger = _room.AddBox<WiredTriggerItemStateUpdated>(1, 0, 0, "wf_trg_state_changed");

        (
            await _room.SaveAsync<UpdateTriggerMessage>(
                1,
                intParams: [currentStateOnly ? 1 : 0],
                stuffIds: [LAMP],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ]
            )
        ).Should().BeTrue();

        // A change away from the saved state first, so a change "to 0" is a real change.
        await lamp.Logic.SetStateAsync(1, false);
        await lamp.Logic.SetStateAsync(newState, false);

        var matched = await trigger.MatchesEventAsync(
            new RoomItemStateChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                ObjectId = LAMP,
            },
            Ct
        );

        matched.Should().Be(fires);
    }
}
