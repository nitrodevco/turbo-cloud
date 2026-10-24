using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Furni movers meeting a user (official client capture 2026-10-24, W1 lab, own room): Relative
/// Furni Movement and Match To Snapshot are blocked by a user on the target tile, with no
/// collision (action.33.X5 3/3, action.3.B2 2/2); Relative Furni Movement jumps straight to its
/// target, the tiles between unchecked (action.33.R3, 2/2). Move As Group puts the corner of the
/// box around the group (smallest x, smallest y) on the target, and a member that cannot move
/// stays while the others move (action.57.R3 4/4, action.57.B1 2/2). The movement physics
/// add-on's "move through users" lets Move Furni To Direction onto a user even with "block furni
/// movements" ticked, the collision still raised (addon.7.X5, 1/1). The clicker (object 5) is
/// the user.
/// </summary>
public sealed class WiredFurniUserBlockTests
{
    private const int CLICK_ME = 20;
    private const int A = 21;
    private const int B = 22;
    private const int SWITCH = 23;
    private const int USER = 5;

    private readonly WiredRoom _room = new(10, 10);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    // action.33.X5 (blocked), action.33.R3 (over the user to the tile beyond).
    [Theory]
    [InlineData(1, 3, 3)]
    [InlineData(2, 5, 3)]
    public async Task Relative_movement_is_blocked_by_a_user_on_its_target_only(
        int dx,
        int x,
        int y
    )
    {
        _room.Enter(USER, 4, 3);
        _room.AddFloorItem(A, 3, 3);

        await BuildAsync(async box =>
        {
            _room.AddBox<WiredActionRelativeMove>(box, 0, 0, "wf_act_rel_mov");

            return await _room.SaveAsync<UpdateActionMessage>(
                box,
                intParams: [dx, 0],
                stuffIds: [A],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ],
                definitionSpecifics: [0]
            );
        });

        await ClickAsync();

        At(A).Should().Be((x, y));
    }

    // action.3.B2
    [Fact]
    public async Task Match_to_snapshot_is_blocked_by_a_user_on_the_snapshot_tile()
    {
        var user = _room.Enter(USER, 1, 1);
        _room.AddFloorItem(A, 3, 3);

        await BuildAsync(async box =>
        {
            _room.AddBox<WiredActionMatchToSnapshot>(box, 0, 0, "wf_act_match_to_sshot");

            return await _room.SaveAsync<UpdateActionMessage>(
                box,
                intParams: [0, 0, 1, 0],
                stuffIds: [A],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ],
                definitionSpecifics: [0]
            );
        });

        _room.Map.MoveFloorItem(_room.FloorItem(A), _room.Map.ToIdx(6, 6));
        Relocate(user, 3, 3);

        await ClickAsync();

        At(A).Should().Be((6, 6));
    }

    // action.57.R3 + B1: the corner (2,2) lands on the user (5,5) and is blocked there; the other
    // member moves by the same delta.
    [Fact]
    public async Task Move_as_group_puts_the_groups_corner_on_the_target_and_moves_the_rest()
    {
        _room.Enter(USER, 5, 5);
        _room.AddFloorItem(A, 3, 4);
        _room.AddFloorItem(B, 2, 2);

        await BuildAsync(async box =>
        {
            _room.AddBox<WiredActionMoveAsGroup>(box, 0, 0, "wf_act_move_furni_as_group");

            return await _room.SaveAsync<UpdateActionMessage>(
                box,
                intParams: [1, 0, 0],
                stuffIds: [A, B],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                    [WiredFurniSourceType.TriggeredItem],
                ],
                playerSources:
                [
                    [WiredPlayerSourceType.TriggeredUser],
                ],
                definitionSpecifics: [0]
            );
        });

        await ClickAsync();

        At(B).Should().Be((2, 2));
        At(A).Should().Be((6, 7));
    }

    // addon.7.X5
    [Fact]
    public async Task Move_through_users_takes_move_to_direction_onto_the_user()
    {
        _room.Enter(USER, 4, 3);
        _room.AddFloorItem(A, 3, 3);
        _room.AddFloorItem(SWITCH, 9, 9);

        await BuildAsync(async box =>
        {
            _room.AddBox<WiredActionMoveToDirection>(box, 0, 0, "wf_act_move_to_dir");
            _room.AddBox<WiredAddonMovementPhysics>(box + 1, 0, 0, "wf_xtra_mov_physics");
            _room.AddBox<WiredTriggerHabboCollision>(box + 2, 1, 0, "wf_trg_collision");
            _room.AddBox<WiredActionToggleItemState>(box + 3, 1, 0, "wf_act_toggle_state");

            return await _room.SaveAsync<UpdateActionMessage>(
                    box,
                    intParams: [(int)Rotation.East, 0, 1],
                    stuffIds: [A],
                    furniSources:
                    [
                        [WiredFurniSourceType.SelectedItems],
                    ]
                )
                && await _room.SaveAsync<UpdateAddonMessage>(box + 1, intParams: [0, 0, 1, 0])
                && await _room.SaveAsync<UpdateTriggerMessage>(box + 2)
                && await _room.SaveAsync<UpdateActionMessage>(
                    box + 3,
                    intParams: [0],
                    stuffIds: [SWITCH],
                    furniSources:
                    [
                        [WiredFurniSourceType.SelectedItems],
                    ],
                    definitionSpecifics: [0]
                );
        });

        await ClickAsync();

        At(A).Should().Be((4, 3));
        _room.FloorItem(SWITCH).Logic.GetState().Should().NotBe(0);
    }

    private (int, int) At(int id) => (_room.FloorItem(id).X, _room.FloorItem(id).Y);

    private void Relocate(Turbo.Rooms.Object.Avatars.Player.RoomPlayerAvatar user, int x, int y)
    {
        _room.Map.RemoveAvatarAtIdx(user, _room.Map.ToIdx(user.X, user.Y), true);
        user.SetPosition(x, y);
        _room.Map.AddAvatarAtIdx(user, _room.Map.ToIdx(x, y), true);
    }

    private async Task ClickAsync()
    {
        await _room
            .FloorItem(CLICK_ME)
            .Logic.OnClickAsync(ActionContext.CreateForPlayer((PlayerId)105, (RoomId)1), 0, Ct);
        await TickAsync(4);
    }

    /// <summary>Click furni -> what <paramref name="effects"/> puts on the stack from box 2 up.</summary>
    private async Task BuildAsync(Func<int, Task<bool>> effects)
    {
        _room.AddFloorItem(CLICK_ME, 9, 0);
        _room.AddBox<WiredTriggerClickFurni>(1, 0, 0, "wf_trg_click_furni");
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

        (await effects(2)).Should().BeTrue();

        await Wired.OnRoomEventAsync(
            new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                StackIds = [_room.Map.ToIdx(0, 0), _room.Map.ToIdx(1, 0)],
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
