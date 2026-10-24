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
/// there sends nothing (action.29.L1, 3/3).
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
