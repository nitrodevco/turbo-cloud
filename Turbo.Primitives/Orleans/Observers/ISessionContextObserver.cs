using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms;

namespace Turbo.Primitives.Orleans.Observers;

/// <summary>
/// The presence grain's line to its player's socket. Only the presence grain's outgoing queue
/// calls it, one flush at a time, so what it delivers arrives in the order it was sent.
/// </summary>
public interface ISessionContextObserver : IGrainObserver
{
    /// <summary>
    /// Records the player's active room on the session, then writes
    /// <paramref name="composers"/> in order in one send. The room is set first so a packet the
    /// client sends in reply to one of these composers is handled in the room the composer came
    /// from. <paramref name="composers"/> may be empty when only the room changed.
    /// </summary>
    /// <param name="composers">
    /// <c>[Immutable]</c>: the presence grain hands over a fresh list per flush and never touches
    /// it again, so the local call need not copy it.
    /// </param>
    public Task SendComposersAsync(
        [Immutable] IReadOnlyList<IComposer> composers,
        RoomId activeRoomId,
        CancellationToken ct = default
    );
}
