using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Orleans.Observers;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Networking;

public interface ISessionGateway
{
    public ISessionContext? GetSession(SessionKey key);

    /// <summary>Every open connection at this moment, logged in or not.</summary>
    public IReadOnlyCollection<ISessionContext> GetSessions();
    public ISessionContextObserver? GetSessionObserver(SessionKey key);
    public PlayerId GetPlayerId(SessionKey key);
    public Task AddSessionAsync(SessionKey key, ISessionContext ctx);
    public Task RemoveSessionAsync(SessionKey key, CancellationToken ct);
    public Task AddSessionToPlayerAsync(SessionKey key, PlayerId playerId);
    public Task RemoveSessionFromPlayerAsync(PlayerId playerId, CancellationToken ct);
}
