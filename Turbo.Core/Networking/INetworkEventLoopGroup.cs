namespace Turbo.Core.Networking;

using DotNetty.Transport.Channels;

public interface INetworkEventLoopGroup
{
    public IEventLoopGroup Boss { get; }

    public IEventLoopGroup Worker { get; }
}
