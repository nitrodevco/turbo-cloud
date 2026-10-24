using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Avatars.Player;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The movement add-ons as Habbo runs them (official client capture 2026-10-24, W1 lab, own
/// room): "no animation" sends the furni's move as a plain ObjectUpdate at the target before the
/// boxes' flash, no WiredMovements (addon.6.R1, 3/3); a user it carries still comes as a
/// WiredMovements user move with no animation time and no furni moves (addon.6.R5, 2/2).
/// Animation Time and Movement Curve keep a saved value out of range at the nearest end
/// (addon.9.E9b 4/4, addon.22.E9b 3/3), and a curve of 0 is no curve (addon.22.E9b).
/// </summary>
public sealed class WiredMovementAddonsTests
{
    private const int CLICK_ME = 20;
    private const int BLOCK = 21;
    private const int USER = 5;
    private const int MOVE_WEST = 7;

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    // addon.6.R1
    [Fact]
    public async Task No_animation_sends_a_plain_update_before_the_flash()
    {
        _room.Enter(USER, 7, 7);
        await BuildAsync(withCarry: false, addon: NoAnimationAsync);

        var sent = await ClickAsync();

        sent.Should().Equal("ObjectUpdate", "ObjectsDataUpdate");
        (_room.FloorItem(BLOCK).X, _room.FloorItem(BLOCK).Y).Should().Be((2, 3));
    }

    // addon.6.R5
    [Fact]
    public async Task No_animation_still_sends_a_carried_user_as_a_movement()
    {
        var user = _room.Enter(USER, 3, 3);
        await SettleAsync(user);
        await BuildAsync(withCarry: true, addon: NoAnimationAsync);

        var before = _room.Harness.Fakes.SentToRoom<WiredMovementsMessageComposer>().Count();
        var sent = await ClickAsync();

        sent.Should().Equal("ObjectUpdate", "ObjectsDataUpdate", "WiredMovements");

        var movements = _room
            .Harness.Fakes.SentToRoom<WiredMovementsMessageComposer>()
            .Skip(before)
            .Single();

        movements.FloorItems.Should().BeEmpty();
        movements
            .Users.Should()
            .ContainSingle()
            .Which.Should()
            .BeEquivalentTo(
                new
                {
                    SourceX = 3,
                    TargetX = 2,
                    AnimationTime = 0,
                }
            );
    }

    // addon.9.E9b
    [Theory]
    [InlineData(10, 50)]
    [InlineData(3000, 2000)]
    [InlineData(175, 175)]
    public async Task Animation_time_keeps_a_value_out_of_range_at_the_nearest_end(
        int saved,
        int stored
    )
    {
        var box = _room.AddBox<WiredAddonAnimationTime>(1, 0, 0, "wf_xtra_anim_time");

        (await _room.SaveAsync<UpdateAddonMessage>(1, intParams: [saved])).Should().BeTrue();

        box.GetSnapshot().IntParams.Should().Equal(stored);
    }

    // addon.22.E9b
    [Theory]
    [InlineData(5000, 1000)]
    [InlineData(-5000, -1000)]
    public async Task Curve_strength_keeps_a_value_out_of_range_at_the_nearest_end(
        int saved,
        int stored
    )
    {
        var box = _room.AddBox<WiredAddonJumpStrength>(1, 0, 0, "wf_xtra_mov_curve");

        (await _room.SaveAsync<UpdateAddonMessage>(1, intParams: [0, saved, -20]))
            .Should()
            .BeTrue();

        box.GetSnapshot().IntParams.Should().Equal(0, stored, -20);
    }

    // addon.22.E9b: 0 is a straight move, the curve strength field left out.
    [Fact]
    public async Task A_curve_of_0_is_no_curve()
    {
        _room.Enter(USER, 7, 7);
        await BuildAsync(
            withCarry: false,
            addon: async box =>
            {
                _room.AddBox<WiredAddonJumpStrength>(box, 0, 0, "wf_xtra_mov_curve");

                return await _room.SaveAsync<UpdateAddonMessage>(box, intParams: [0, 0, -20]);
            }
        );

        var before = _room.Harness.Fakes.SentToRoom<WiredMovementsMessageComposer>().Count();

        await ClickAsync();

        _room
            .Harness.Fakes.SentToRoom<WiredMovementsMessageComposer>()
            .Skip(before)
            .Single()
            .FloorItems.Should()
            .ContainSingle()
            .Which.CurveStrength.Should()
            .BeNull();
    }

    private async Task<bool> NoAnimationAsync(int box)
    {
        _room.AddBox<WiredAddonCancelAnimationTime>(box, 0, 0, "wf_xtra_mov_no_animation");

        return await _room.SaveAsync<UpdateAddonMessage>(box);
    }

    private async Task SettleAsync(RoomPlayerAvatar user)
    {
        await _room.Map.InvokeAvatarAsync(user, Ct);

        _room.Clock.AdvanceMs(500);
        await _room.Harness.Room.AvatarTickSystem.ProcessAvatarsAsync(_room.Harness.NowMs(), Ct);
    }

    /// <summary>
    /// Click furni -> move the block at 3,3 west, with <paramref name="addon"/> and, if asked,
    /// the carry-users add-on (users on the furni, everyone).
    /// </summary>
    private async Task BuildAsync(bool withCarry, Func<int, Task<bool>> addon)
    {
        _room.AddFloorItem(CLICK_ME, 7, 4);
        _room.AddFloorItem(BLOCK, 3, 3);
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

        _room.AddBox<WiredActionMoveRotateFurni>(2, 0, 0, "wf_act_move_rotate");
        (
            await _room.SaveAsync<UpdateActionMessage>(
                2,
                intParams: [MOVE_WEST, 0],
                stuffIds: [BLOCK],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();

        (await addon(3)).Should().BeTrue();

        if (withCarry)
        {
            _room.AddBox<WiredAddonCarryUsers>(4, 0, 0, "wf_xtra_mov_carry_users");
            (
                await _room.SaveAsync<UpdateAddonMessage>(
                    4,
                    intParams: [(int)WiredCarryUserType.StandingOnFurni],
                    playerSources:
                    [
                        [WiredPlayerSourceType.AllRoomUsers],
                    ]
                )
            ).Should().BeTrue();
        }

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
    }

    /// <summary>What the room was sent about the block and the boxes for one click, in order.</summary>
    private async Task<List<string>> ClickAsync()
    {
        var before = _room.Harness.Fakes.Sent(SendRoute.Room).Count();

        await _room
            .FloorItem(CLICK_ME)
            .Logic.OnClickAsync(ActionContext.CreateForPlayer((PlayerId)105, (RoomId)1), 0, Ct);
        await TickAsync(3);

        return
        [
            .. _room
                .Harness.Fakes.Sent(SendRoute.Room)
                .Skip(before)
                .Select(x =>
                    x switch
                    {
                        ObjectUpdateMessageComposer => "ObjectUpdate",
                        ObjectsDataUpdateMessageComposer => "ObjectsDataUpdate",
                        WiredMovementsMessageComposer => "WiredMovements",
                        _ => null,
                    }
                )
                .OfType<string>(),
        ];
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
