using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Orleans.Observers;

namespace Turbo.Primitives.Networking;

public interface ISessionGateway
{
    public ISessionContext? GetSession(SessionKey key);
    public ISessionContextObserver? GetSessionObserver(SessionKey key);
    public long GetPlayerId(SessionKey key);
    public Task AddSessionAsync(SessionKey key, ISessionContext ctx);
    public Task RemoveSessionAsync(SessionKey key, CancellationToken ct);
    public Task AddSessionToPlayerAsync(SessionKey key, long playerId);
    public Task RemoveSessionFromPlayerAsync(long playerId, CancellationToken ct);
}
