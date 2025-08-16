using System;
using DotNetty.Codecs.Http;
using DotNetty.Codecs.Http.WebSockets;
using DotNetty.Transport.Channels;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Networking.Codec;
using Turbo.Networking.Servers.Handlers;

namespace Turbo.Networking.Servers.Websocket;

public class WebsocketChannelInitializer(IServiceProvider provider) : ChannelInitializer<IChannel>
{
    private readonly IServiceProvider _provider = provider;

    protected override void InitChannel(IChannel channel)
    {
        channel
            .Pipeline.AddLast("httpCodec", new HttpServerCodec())
            .AddLast("objectAggregator", new HttpObjectAggregator(65536))
            .AddLast("wsProtocolHandler", new WebSocketServerProtocolHandler("/", true))
            .AddLast("websocketCodec", new WebsocketCodec())
            .AddLast("frameEncoder", new FrameLengthFieldEncoder())
            .AddLast("frameDecoder", new FrameLengthFieldDecoder())
            .AddLast("gameEncoder", new GameEncoder())
            .AddLast("gameDecoder", new GameDecoder())
            .AddLast("inbound", ActivatorUtilities.CreateInstance<InboundHandler>(_provider));
    }
}
