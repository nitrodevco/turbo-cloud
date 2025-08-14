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

namespace Turbo.Networking.Hosts.Websocket;

public class WebsocketServer : IServer
{
    private readonly INetworkHostConfig _config;
    private readonly INetworkEventLoopGroup _eventLoopGroup;
    private readonly ILogger<WebsocketServer> _logger;
    private readonly IServiceProvider _provider;
    private readonly ServerBootstrap _serverBootstrap;

    public string Host { get; }
    public int Port { get; }
    public IChannel ServerChannel { get; private set; }

    public WebsocketServer(
        INetworkHostConfig config,
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
            _serverBootstrap.Group(_eventLoopGroup.Group);
            _serverBootstrap.Channel<TcpServerSocketChannel>();
            _serverBootstrap.ChildOption(ChannelOption.TcpNodelay, true);
            _serverBootstrap.ChildOption(ChannelOption.SoKeepalive, true);
            _serverBootstrap.ChildOption(ChannelOption.SoReuseaddr, true);
            _serverBootstrap.ChildOption(ChannelOption.SoRcvbuf, 4096);
            _serverBootstrap.ChildOption(ChannelOption.RcvbufAllocator, new FixedRecvByteBufAllocator(4096));
            _serverBootstrap.ChildOption(ChannelOption.Allocator, new UnpooledByteBufferAllocator(false));
            _serverBootstrap.ChildHandler(new WebsocketChannelInitializer(_provider));

            ServerChannel = await _serverBootstrap.BindAsync(IPAddress.Parse(Host), Port);

            _logger.LogInformation("{Context} -> Listening on ws://{Host}:{Port}", nameof(WebsocketServer), Host, Port);
        }
        catch (SocketException ex)
        {
            _logger.LogError(ex, "{Context} -> Failed to start WebSocket server on {Host}:{Port}", nameof(WebsocketServer), Host, Port);
            throw;
        }
    }

    public async Task ShutdownAsync()
    {
        await ServerChannel.CloseAsync();
    }
}