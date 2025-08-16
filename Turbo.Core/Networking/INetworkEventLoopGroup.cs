using DotNetty.Transport.Channels;

namespace Turbo.Core.Networking;

public interface INetworkEventLoopGroup
{
    public IEventLoopGroup Boss { get; }

    public IEventLoopGroup Worker { get; }
}
