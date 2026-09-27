using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms;

namespace Turbo.Networking.Session;

/// <summary>
/// What the session observer needs from a session beyond <see cref="ISessionContext"/>: the
/// batched send the presence grain's flush uses, and the active room it pushes. Kept off
/// <see cref="ISessionContext"/> so handlers keep one way to send, and only the presence grain
/// sets the room.
/// </summary>
internal interface ISessionOutbound
{
    public void SetActiveRoomId(RoomId roomId);

    public Task SendComposersAsync(IReadOnlyList<IComposer> composers, CancellationToken ct);
}
