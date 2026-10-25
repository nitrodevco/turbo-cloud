using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Games;

/// <summary>What changed in the room's game.</summary>
public enum RoomGameChange
{
    /// <summary>A game started: the scores are back at 0.</summary>
    Started,

    /// <summary>The running game ended.</summary>
    Ended,

    /// <summary>Someone joined or left a team.</summary>
    Teams,

    /// <summary>A team's score changed.</summary>
    Score,
}

/// <summary>
/// A furni that shows or plays the room's game (<see cref="Grains.Systems.RoomGameSystem"/>): the
/// team gates, the team scoreboards, the Battle Banzai tiles, and whatever a plugin adds. The
/// game tells each one what changed; the ones whose state changed go to the room together, in one
/// <c>ObjectsDataUpdate</c>: when a player joined green, the Banzai and the Freeze green gates
/// went from 3 to 4 in one such packet (official client capture 2026-10-17T22-46-19, room
/// 80317230).
/// </summary>
public interface IRoomGameFurni
{
    /// <summary>
    /// Brings the furni up to date. Its new state is set without telling the room (a state set
    /// with <c>refresh: false</c>); true when it changed, so the game sends it with the others.
    /// </summary>
    public Task<bool> OnGameChangedAsync(RoomGameChange change, CancellationToken ct);
}
