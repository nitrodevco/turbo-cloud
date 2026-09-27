using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Turbo.Logging;
using Turbo.Pipeline;
using Turbo.Primitives;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms;

namespace Turbo.Messages.Registry;

public sealed class MessageRegistry(IServiceProvider sp, ILogger<MessageRegistry> logger)
    : EnvelopeHost<IMessageEvent, ISessionContext, MessageContext>(
        sp,
        new EnvelopeHostOptions<IMessageEvent, ISessionContext, MessageContext>
        {
            CreateContextAsync = CreateContextFactory(sp),
            EnableInheritanceDispatch = true,
            HandlerMode = HandlerExecutionMode.Parallel,
            MaxHandlerDegreeOfParallelism = null,
        },
        logger
    )
{
    // Runs for every incoming packet, so it touches no grain: the room comes from the session,
    // where the presence grain pushes it ahead of the composers it sends (see
    // ISessionContextObserver). The gateway is resolved on first use, not per packet, and not in
    // the constructor, which runs while the host is still being built.
    private static Func<IMessageEvent, ISessionContext?, Task<MessageContext>> CreateContextFactory(
        IServiceProvider sp
    )
    {
        ISessionGateway? sessionGateway = null;

        return (env, data) =>
        {
            if (data is null)
                throw new TurboException(TurboErrorCodeEnum.InvalidSession);

            sessionGateway ??= sp.GetRequiredService<ISessionGateway>();

            var playerId = sessionGateway.GetPlayerId(data.SessionKey);
            RoomId roomId = playerId > 0 ? data.ActiveRoomId : -1;

            return Task.FromResult(new MessageContext(data, playerId, roomId));
        };
    }
}
