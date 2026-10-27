using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Policies;
using Turbo.Primitives.Rooms;

namespace RoomWindow;

/// <summary>
/// A player who came through a window appears on the tile they clicked, not at the door. The
/// window put that tile in the entry extension (<see cref="RoomWindowLogic"/>); the room asks this
/// policy as it makes their avatar, after every entry check has let them in. A tile they cannot
/// stand on means the door, as core decides.
/// </summary>
public sealed class WindowArrival : IRoomArrivalPolicy
{
    public ValueTask<RoomArrivalDecision> DecideAsync(
        RoomArrivalRequest request,
        RoomArrivalDecision proposed,
        CancellationToken ct
    )
    {
        if (request.Extension is not { Key: RoomWindowLogic.EXTENSION } extension)
            return ValueTask.FromResult(proposed);

        var tile = JsonSerializer.Deserialize<RoomTilePoint>(extension.Payload);

        return ValueTask.FromResult(proposed.StartAt(tile.X, tile.Y, proposed.Rotation));
    }
}
