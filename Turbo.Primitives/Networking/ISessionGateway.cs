using Turbo.Primitives.Orleans.Snapshots.Session;

namespace Turbo.Primitives.Networking;

public interface ISessionGateway
{
    public ISessionContext? GetSession(SessionKey key);
    public void AddSession(SessionKey key, ISessionContext ctx);
    public void RemoveSession(SessionKey key);
    public long GetPlayerId(SessionKey key);
    public void BindSessionToPlayer(SessionKey key, long playerId);
    public void UnbindSessionFromPlayer(SessionKey key);
}
