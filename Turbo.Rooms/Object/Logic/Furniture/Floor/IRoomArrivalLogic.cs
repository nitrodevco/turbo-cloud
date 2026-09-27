using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Object.Avatars;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor;

/// <summary>
/// A furni a player can arrive in from another room (a teleporter's far half). The avatar
/// module asks this rather than naming the furni, and starts the player on its tile instead of
/// the door when it can take them.
/// </summary>
public interface IRoomArrivalLogic
{
    /// <summary>Whether it can take an arriving player now; one that is busy cannot.</summary>
    public bool CanReceiveArrival { get; }

    /// <summary>
    /// The player has just been placed on its tile: it takes them from here (for a teleporter,
    /// flashing, opening, and letting them walk out).
    /// </summary>
    public Task ReceiveArrivalAsync(IRoomAvatar avatar, CancellationToken ct);
}
