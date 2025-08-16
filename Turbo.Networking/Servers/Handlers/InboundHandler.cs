namespace Turbo.Networking.Servers.Handlers;

using System;

using DotNetty.Common.Utilities;
using DotNetty.Transport.Channels;

using Microsoft.Extensions.Logging;

using Turbo.Core.Networking;
using Turbo.Core.Networking.Dispatcher;
using Turbo.Core.Networking.Session;
using Turbo.Core.Packets.Messages;
using Turbo.Networking.Session;

public class InboundHandler(
    ISessionManager sessionManager,
    IPacketDispatcher packetDispatcher,
    ILogger<InboundHandler> logger) : ChannelHandlerAdapter
{
    private readonly ISessionManager _sessionManager = sessionManager;
    private readonly IPacketDispatcher _packetDispatcher = packetDispatcher;
    private readonly ILogger<InboundHandler> _logger = logger;

    public override void ChannelActive(IChannelHandlerContext ctx)
    {
        _sessionManager.CreateSession(ctx);

        base.ChannelActive(ctx);
    }

    public override void ChannelInactive(IChannelHandlerContext ctx)
    {
        _packetDispatcher.ResetForChannelId(ctx.Channel.Id);
        _sessionManager.RemoveSessionById(ctx.Channel.Id, out _);

        base.ChannelInactive(ctx);
    }

    public override void ChannelRead(IChannelHandlerContext ctx, object message)
    {
        if (message is not IClientPacket packet)
        {
            ReferenceCountUtil.Release(message);

            return;
        }

        var buffer = packet.Content;

        ReferenceCountUtil.Retain(buffer);

        if (!_packetDispatcher.TryEnqueue(ctx.Channel.Id, packet, out var reason))
        {
            _logger.LogDebug("Drop packet header={Header} channelId={Sid} reason={Reason}", packet.Header, ctx.Channel.Id, reason);

            ReferenceCountUtil.SafeRelease(buffer);

            return;
        }
    }
}
