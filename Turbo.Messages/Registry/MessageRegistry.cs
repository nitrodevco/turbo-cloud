using System;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orleans;
using Turbo.Logging;
using Turbo.Pipeline;
using Turbo.Primitives;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;

namespace Turbo.Messages.Registry;

public sealed class MessageRegistry(IServiceProvider sp, ILogger<MessageRegistry> logger)
    : EnvelopeHost<IMessageEvent, ISessionContext, MessageContext>(
        sp,
        new EnvelopeHostOptions<IMessageEvent, ISessionContext, MessageContext>
        {
            CreateContextAsync = async (env, data) =>
            {
                if (data is null)
                    throw new TurboException(TurboErrorCodeEnum.InvalidSession);

                var grainFactory = sp.GetRequiredService<IGrainFactory>();
                var sessionGateway = sp.GetRequiredService<ISessionGateway>();
                var playerId = sessionGateway.GetPlayerId(data.SessionKey);
                var roomId = -1;

                if (playerId > 0)
                {
                    var playerPresence = grainFactory.GetPlayerPresenceGrain(playerId);
                    var activeRoom = await playerPresence
                        .GetActiveRoomAsync(CancellationToken.None)
                        .ConfigureAwait(false);

                    roomId = activeRoom.RoomId;
                }

                return new(data, playerId, roomId);
            },
            EnableInheritanceDispatch = true,
            HandlerMode = HandlerExecutionMode.Parallel,
            MaxHandlerDegreeOfParallelism = null,
        },
        logger
    ) { }
