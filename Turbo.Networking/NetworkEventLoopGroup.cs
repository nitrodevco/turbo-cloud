namespace Turbo.Networking;

using DotNetty.Transport.Channels;

using Turbo.Core.Configuration;
using Turbo.Core.Networking;

public class NetworkEventLoopGroup(IEmulatorConfig config) : INetworkEventLoopGroup
{
    public IEventLoopGroup Boss { get; } = new MultithreadEventLoopGroup(config.Network.BossEventLoopThreads);

    public IEventLoopGroup Worker { get; } = new MultithreadEventLoopGroup(config.Network.WorkerEventLoopThreads);
}
