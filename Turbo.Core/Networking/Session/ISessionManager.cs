using System.Threading.Tasks;
using DotNetty.Transport.Channels;

namespace Turbo.Core.Networking.Session;

public interface ISessionManager
{
    public bool TryGetSession(IChannelId id, out ISession session);
    public bool TryRegisterSession(IChannelId id, in ISession session);
    public Task DisconnectSession(IChannelId id);
}