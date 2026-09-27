using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans.Observers;
using Turbo.Primitives.Rooms;

namespace Turbo.Networking.Session;

public sealed class SessionContextObserver(SessionKey sessionKey, ISessionGateway sessionGateway)
    : ISessionContextObserver
{
    private readonly SessionKey _sessionKey = sessionKey;
    private readonly ISessionGateway _sessionGateway = sessionGateway;

    public Task SendComposersAsync(
        IReadOnlyList<IComposer> composers,
        RoomId activeRoomId,
        CancellationToken ct
    )
    {
        if (_sessionGateway.GetSession(_sessionKey) is not ISessionOutbound session)
            return Task.CompletedTask;

        session.SetActiveRoomId(activeRoomId);

        return composers.Count == 0
            ? Task.CompletedTask
            : session.SendComposersAsync(composers, ct);
    }
}
