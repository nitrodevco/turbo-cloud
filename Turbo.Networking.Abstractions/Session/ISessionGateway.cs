using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Abstractions;

namespace Turbo.Networking.Abstractions.Session;

public interface ISessionGateway
{
    public void RegisterSession(ISessionContext session);
    public void UnregisterSession(string connectionId);
    public string? GetConnectionIdByPlayerId(long playerId);
    public ISessionContext? GetSessionContextByConnectionId(string connectionId);
    public ISessionContext? GetSessionContextByPlayerId(long playerId);
    public Task SetPlayerIdForSessionAsync(long playerId, string connectionId);
    public Task SendComposerToPlayerAsync(
        IComposer composer,
        long playerId,
        CancellationToken ct = default
    );
}
