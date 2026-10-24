using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Set Furni Altitude at its limits (official client capture 2026-10-24, W1 lab, own room, a
/// block 1.00 high): a saved altitude out of 0..8000 is kept at the nearest end, not refused
/// (action.29.E1, 3/3); "set 80.00" stops the block's top at 80.00, z 79.00, and an increase
/// there sends nothing (action.29.L1, 3/3); moved in the same firing, the furni still ends at
/// the set altitude (action.29.X1, 16/16); a user standing on it goes up or down with it in the
/// furni's WiredMovements, carry add-on or not (action.29.R2, 4/4: 1 with it, 3 without).
/// </summary>
public sealed class WiredSetAltitudeLabTests
{
    private const int CLICK_ME = 23;
    private const int BLOCK = 24;
    private const int USER = 5;

    private const int INCREASE = 0;
    private const int SET = 2;

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    public WiredSetAltitudeLabTests()
    {
        _room.AddFloorItem(CLICK_ME, 7, 4);
        _room.AddFloorItem(BLOCK, 5, 5);
    }

    // action.29.E1 + L1: 9000 is kept as 8000, and 80.00 stops the block's top at 80.00.
    [Theory]
    [InlineData(9000, 7900)]
    [InlineData(8000, 7900)]
    [InlineData(-300, 0)]
    public async Task An_altitude_out_of_range_is_kept_at_the_nearest_end(int saved, int z)
    {
        _room.Enter(USER, 1, 1);
        await BuildAsync(saved, SET);
        _room.FloorItem(BLOCK).SetPositionZ(Altitude.FromInt(200));

        await ClickAsync();

        _room.FloorItem(BLOCK).Z.ToInt().Should().Be(z);
    }

    // action.29.L1: already at the top, an increase sends nothing.
    [Fact]
    public async Task An_increase_at_the_top_sends_nothing()
    {
        _room.Enter(USER, 1, 1);
        await BuildAsync(5000, INCREASE);
        _room.FloorItem(BLOCK).SetPositionZ(Altitude.FromInt(7900));

        var packets = await ClickAsync();

        packets.Should().BeEmpty();
        _room.FloorItem(BLOCK).Z.ToInt().Should().Be(7900);
    }

    // action.29.X1: moved and given an altitude in one firing, the furni always ends at that
    // altitude on its new tile, whichever box runs first.
    [Fact]
    public async Task A_moved_furni_ends_at_the_altitude_set_in_the_same_firing()
    {
        _room.Enter(USER, 1, 1);
        _room.AddBox<WiredActionRelativeMove>(3, 0, 0, "wf_act_rel_mov");
        (
            await _room.SaveAsync<UpdateActionMessage>(
                3,
                intParams: [0, 2],
                stuffIds: [BLOCK],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();
        await BuildAsync(200, SET);

        for (var i = 0; i < 12; i++)
        {
            _room.Map.MoveFloorItem(_room.FloorItem(BLOCK), _room.Map.ToIdx(5, 3));
            _room.FloorItem(BLOCK).SetPositionZ(Altitude.Zero);

            await ClickAsync();

            (_room.FloorItem(BLOCK).Y, _room.FloorItem(BLOCK).Z.ToInt())
                .Should()
                .Be((5, 200), $"firing {i + 1}");
        }
    }

    // action.29.R2, without the carry add-on.
    [Fact]
    public async Task A_user_standing_on_the_block_goes_up_with_it()
    {
        var user = _room.Enter(USER, 5, 5);
        await _room.Map.InvokeAvatarAsync(user, Ct);
        _room.Clock.AdvanceMs(500);
        await _room.Harness.Room.AvatarTickSystem.ProcessAvatarsAsync(_room.Harness.NowMs(), Ct);
        await BuildAsync(200, SET);

        var packets = await ClickAsync();

        packets.Should().ContainSingle();
        packets[0].FloorItems.Should().ContainSingle(x => (int)x.ObjectId == BLOCK);
        packets[0]
            .Users.Should()
            .ContainSingle(x => (int)x.ObjectId == USER && x.SourceX == 5 && x.TargetX == 5);
        user.Z.ToInt().Should().Be(300);
    }

    private async Task<List<WiredMovementsMessageComposer>> ClickAsync()
    {
        var before = _room.Harness.Fakes.SentToRoom<WiredMovementsMessageComposer>().Count();

        await _room
            .FloorItem(CLICK_ME)
            .Logic.OnClickAsync(ActionContext.CreateForPlayer((PlayerId)105, (RoomId)1), 0, Ct);
        await TickAsync(4);

        return [.. _room.Harness.Fakes.SentToRoom<WiredMovementsMessageComposer>().Skip(before)];
    }

    private async Task BuildAsync(int altitude, int op)
    {
        _room.AddBox<WiredTriggerClickFurni>(1, 0, 0, "wf_trg_click_furni");
        _room.AddBox<WiredActionSetAltitude>(2, 0, 0, "wf_act_set_altitude");

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
                intParams: [altitude, op],
                stuffIds: [BLOCK],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ]
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
