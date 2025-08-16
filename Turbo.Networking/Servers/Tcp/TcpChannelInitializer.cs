using System;
using DotNetty.Transport.Channels;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Networking.Codec;
using Turbo.Networking.Servers.Handlers;

namespace Turbo.Networking.Servers.Tcp;

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
            .AddLast("inbound", ActivatorUtilities.CreateInstance<InboundHandler>(_provider));
    }
}