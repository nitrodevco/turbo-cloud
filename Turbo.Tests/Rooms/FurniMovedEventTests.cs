using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Events.RoomItem;
using Turbo.Primitives.Rooms.Object;
using Turbo.Rooms.Grains.Modules;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The moved event a furni raises says whether it left its tile: a floor item turned in place
/// is a rotation (reward track <c>rotate_item</c>), one put on another tile a move (<c>move_item</c>).
/// </summary>
public sealed class FurniMovedEventTests
{
    private const int CRATE = 20;

    private readonly WiredRoom _room = new(8, 8);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Turning_a_furni_in_place_is_not_a_tile_change_and_moving_it_is()
    {
        _room.AddFloorItem(CRATE, 2, 2);
        var heard = new RecordingListener();
        using var registration = _room.Harness.EventListeners.Register([heard]);
        var furni = _room.Harness.Module<RoomFurniModule>();
        var player = ActionContext.CreateForPlayer((PlayerId)1, (RoomId)1);

        await furni.MoveFloorItemByIdAsync(player, CRATE, 2, 2, null, Rotation.East, Ct);
        await furni.MoveFloorItemByIdAsync(player, CRATE, 3, 2, null, Rotation.East, Ct);

        heard.Moves.Select(x => x.TileChanged).Should().Equal(false, true);
    }

    private sealed class RecordingListener : IRoomEventListener
    {
        public List<RoomItemMovedEvent> Moves { get; } = [];

        public Task OnRoomEventAsync(RoomEvent evt, CancellationToken ct)
        {
            if (evt is RoomItemMovedEvent moved)
                Moves.Add(moved);

            return Task.CompletedTask;
        }
    }
}
