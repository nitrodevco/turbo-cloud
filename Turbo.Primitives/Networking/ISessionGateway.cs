using System.Threading.Tasks;
using Turbo.Primitives.Orleans.Observers;
using Turbo.Primitives.Orleans.Snapshots.Session;

namespace Turbo.Primitives.Networking;

public interface ISessionGateway
{
    public ISessionContext? GetSession(SessionKey key);
    public Task AddSessionAsync(SessionKey key, ISessionContext ctx);
    public Task RemoveSessionAsync(SessionKey key);
    public ISessionContextObserver? GetSessionObserver(SessionKey key);
    public Task AddSessionToPlayerAsync(SessionKey key, long playerId);
    public Task RemoveSessionFromPlayerAsync(long playerId);
    public long GetPlayerId(SessionKey key);
    public void BindSessionToPlayer(SessionKey key, long playerId);
    public void UnbindSessionFromPlayer(SessionKey key);
}
