using DotNetty.Transport.Channels;
using Turbo.Core.Configuration;
using Turbo.Core.Networking;
using Turbo.Core.Networking.Session;

namespace Turbo.Networking.Factories;

public interface ISessionFactory
{
    public ISession CreateSession(IChannelHandlerContext context);
}