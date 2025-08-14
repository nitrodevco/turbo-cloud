using System;
using DotNetty.Transport.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Turbo.Core.Networking.Session;
using Turbo.Networking.Codec;
using Turbo.Networking.Factories;
using Turbo.Networking.Handlers;

namespace Turbo.Networking.Hosts.Tcp;

public class TcpChannelInitializer(IServiceProvider provider) : ChannelInitializer<IChannel>
{
    private readonly IServiceProvider _provider = provider;

    protected override void InitChannel(IChannel channel)
    {
        channel.Pipeline
            .AddLast("flashPolicy", new FlashPolicyHandler())
            .AddLast("frameEncoder", new FrameLengthFieldEncoder())
            .AddLast("frameDecoder", new FrameLengthFieldDecoder())
            .AddLast("gameEncoder", new GameEncoder())
            .AddLast("gameDecoder", new GameDecoder())
            .AddLast("messageHandler", new GameMessageHandler(
                _provider.GetRequiredService<ISessionManager>(),
                _provider.GetRequiredService<ISessionFactory>(),
                _provider.GetService<ILogger<GameMessageHandler>>())
        );
    }
}