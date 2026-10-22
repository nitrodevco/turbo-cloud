using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Rooms;

/// <summary>
/// Gives a room to another player. The one way to change a room's owner: the room row, its rights
/// and a loaded room's state change together (see <c>IRoomGrain.TransferOwnershipAsync</c>), where
/// writing the row alone leaves a loaded room showing its old owner until it unloads.
/// </summary>
public interface IRoomOwnershipService
{
    /// <summary>
    /// Makes <paramref name="newOwnerId"/> the owner of <paramref name="roomId"/>. The caller
    /// decides who may ask and keeps its own record of who did; this checks that both exist.
    /// </summary>
    public Task<RoomOwnerTransferOutcome> TransferAsync(
        RoomId roomId,
        PlayerId newOwnerId,
        CancellationToken ct
    );
}
