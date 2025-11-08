using System.Threading.Tasks;

namespace Turbo.Networking.Abstractions.Session;

public interface ISessionGateway
{
    public void Register(string connectionId, ISessionContext session);
    public void Unregister(string connectionId);
    public Task SetPlayerIdForSessionAsync(long playerId, string connectionId);
}
