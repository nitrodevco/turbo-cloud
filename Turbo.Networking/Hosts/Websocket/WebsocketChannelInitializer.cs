using System;
using DotNetty.Codecs.Http;
using DotNetty.Codecs.Http.WebSockets;
using DotNetty.Transport.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Turbo.Core.Networking.Session;
using Turbo.Networking.Codec;
using Turbo.Networking.Factories;
using Turbo.Networking.Handlers;

namespace Turbo.Networking.Hosts.Websocket;

public class WebsocketChannelInitializer(IServiceProvider provider) : ChannelInitializer<IChannel>
{
    private readonly IServiceProvider _provider = provider;

    protected override void InitChannel(IChannel channel)
    {
        channel.Pipeline
            .AddLast("httpCodec", new HttpServerCodec())
            .AddLast("objectAggregator", new HttpObjectAggregator(65536))
            .AddLast("wsProtocolHandler", new WebSocketServerProtocolHandler("/", true))
            .AddLast("websocketCodec", new WebsocketCodec())
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