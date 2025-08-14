using DotNetty.Transport.Channels;
using Turbo.Core.Configuration;
using Turbo.Core.Networking;

namespace Turbo.Networking;

public class NetworkEventLoopGroup(IEmulatorConfig config) : INetworkEventLoopGroup
{
    public IEventLoopGroup Group { get; } = new MultithreadEventLoopGroup(config.Network.NetworkWorkerThreads);
}