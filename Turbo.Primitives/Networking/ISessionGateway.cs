using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Orleans.Observers;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Networking;

public interface ISessionGateway
{
    public ISessionContext? GetSession(SessionKey key);
    public ISessionContextObserver? GetSessionObserver(SessionKey key);
    public PlayerId GetPlayerId(SessionKey key);
    public Task AddSessionAsync(SessionKey key, ISessionContext ctx);
    public Task RemoveSessionAsync(SessionKey key, CancellationToken ct);
    public Task AddSessionToPlayerAsync(SessionKey key, PlayerId playerId);
    public Task RemoveSessionFromPlayerAsync(PlayerId playerId, CancellationToken ct);
}
