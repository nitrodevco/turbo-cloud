using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// "Move user to furni" moves the user onto the furni, and its option says what happens to a walk
/// they were on (<c>wiredfurni.params.user_move.walkmode.0</c> to <c>.2</c>): keep walking if the
/// move took them closer to where they were going, keep walking, or stop. The user walks from
/// (1,1) towards (6,1) when the box moves them.
/// </summary>
public sealed class WiredMoveUserToFurniTests
{
    private const int CLICK_ME = 23;
    private const int CLOSER = 24;
    private const int FURTHER = 25;

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    [Theory]
    [InlineData(0, CLOSER, 4, 1, true)]
    [InlineData(0, FURTHER, 0, 6, false)]
    [InlineData(1, FURTHER, 0, 6, true)]
    [InlineData(2, CLOSER, 4, 1, false)]
    public async Task The_user_is_moved_onto_the_furni_and_the_walk_goes_on_or_stops(
        int mode,
        int furni,
        int x,
        int y,
        bool keepsWalking
    )
    {
        var user = _room.Enter(5, 1, 1);
        _room.AddFloorItem(CLICK_ME, 7, 7);
        _room.AddFloorItem(CLOSER, 4, 1);
        _room.AddFloorItem(FURTHER, 0, 6);
        _room.AddBox<WiredTriggerClickFurni>(1, 0, 0, "wf_trg_click_furni");
        _room.AddBox<WiredActionUserToFurni>(2, 0, 0, "wf_act_user_to_furni");

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
                intParams: [(int)mode],
                stuffIds: [furni],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ],
                playerSources:
                [
                    [WiredPlayerSourceType.TriggeredUser],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();

        await Wired.OnRoomEventAsync(
            new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                StackIds = [_room.Map.ToIdx(0, 0)],
            },
            Ct
        );
        await TickAsync(1);

        (await _room.Harness.Module<RoomAvatarModule>().WalkAvatarToAsync(user, 6, 1, Ct))
            .Should()
            .BeTrue();

        await _room
            .FloorItem(CLICK_ME)
            .Logic.OnClickAsync(ActionContext.CreateForPlayer((PlayerId)105, (RoomId)1), 0, Ct);
        await TickAsync(3);

        (user.X, user.Y).Should().Be((x, y));
        user.IsWalking.Should().Be(keepsWalking);

        if (keepsWalking)
            user.GoalTileId.Should().Be(_room.Map.ToIdx(6, 1));
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
