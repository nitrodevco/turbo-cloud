using System;
using System.IO;
using DotNetty.Transport.Channels;
using Microsoft.Extensions.Logging;
using Turbo.Core.Networking.Packets;
using Turbo.Core.Networking.Session;
using Turbo.Networking.Factories;

namespace Turbo.Networking.Handlers;

public class GameMessageHandler(
    ISessionManager sessionManager,
    ISessionFactory sessionFactory,
    ILogger<GameMessageHandler> logger) : SimpleChannelInboundHandler<IClientPacket>
{
    private readonly ISessionManager _sessionManager = sessionManager;
    private readonly ISessionFactory _sessionFactory = sessionFactory;
    private readonly ILogger<GameMessageHandler> _logger = logger;

    public override void ChannelActive(IChannelHandlerContext context)
    {
        _sessionManager.TryRegisterSession(context.Channel.Id,
            _sessionFactory.CreateSession(context));
    }

    public override void ChannelInactive(IChannelHandlerContext context)
    {
        _sessionManager.DisconnectSession(context.Channel.Id);
    }

    protected override void ChannelRead0(IChannelHandlerContext ctx, IClientPacket msg)
    {
        if (!_sessionManager.TryGetSession(ctx.Channel.Id, out var session))
        {
            _logger.LogInformation("Session not found for {}", ctx.Channel.RemoteAddress);
            return;
        }

        session.OnMessageReceived(msg);
    }

    public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
    {
        if (exception is IOException) return;
        _logger.LogError(exception.Message);
    }
}