namespace Turbo.Networking.Servers.Websocket;

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

using DotNetty.Buffers;
using DotNetty.Transport.Bootstrapping;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Sockets;

using Microsoft.Extensions.Logging;

using Turbo.Core.Configuration;
using Turbo.Core.Networking;
using Turbo.Core.Networking.Servers;

public class WebsocketServer : IServer
{
    private readonly INetworkServerConfig _config;
    private readonly INetworkEventLoopGroup _eventLoopGroup;
    private readonly ILogger<WebsocketServer> _logger;
    private readonly IServiceProvider _provider;
    private readonly ServerBootstrap _serverBootstrap;

    public string Host { get; }

    public int Port { get; }

    public IChannel ServerChannel { get; private set; }

    public WebsocketServer(
        INetworkServerConfig config,
        INetworkEventLoopGroup eventLoopGroup,
        ILogger<WebsocketServer> logger,
        IServiceProvider provider)
    {
        _config = config;
        _eventLoopGroup = eventLoopGroup;
        _logger = logger;
        _provider = provider;
        _serverBootstrap = new ServerBootstrap();

        Host = _config.Host;
        Port = _config.Port;
    }

    public async Task StartAsync()
    {
        try
        {
            _serverBootstrap
                .Group(_eventLoopGroup.Boss, _eventLoopGroup.Worker)
                .Channel<TcpServerSocketChannel>()
                .Option(ChannelOption.SoBacklog, 512)
                .ChildOption(ChannelOption.TcpNodelay, true)
                .ChildOption(ChannelOption.SoKeepalive, true)
                .ChildOption(ChannelOption.SoReuseaddr, true)
                .ChildOption(ChannelOption.SoRcvbuf, 4096)
                .ChildOption(ChannelOption.RcvbufAllocator, new FixedRecvByteBufAllocator(4096))
                .ChildOption(ChannelOption.Allocator, new UnpooledByteBufferAllocator(false))
                .ChildHandler(new WebsocketChannelInitializer(_provider));

            ServerChannel = await _serverBootstrap.BindAsync(IPAddress.Parse(Host), Port);

            _logger.LogInformation("{Context} -> Listening on ws://{Host}:{Port}", nameof(WebsocketServer), Host, Port);
        }
        catch (SocketException ex)
        {
            _logger.LogError(ex, "{Context} -> Failed to start WebSocket server on {Host}:{Port}", nameof(WebsocketServer), Host, Port);
            throw;
        }
    }

    public async Task StopAsync()
    {
        await ServerChannel.CloseAsync();
    }
}
