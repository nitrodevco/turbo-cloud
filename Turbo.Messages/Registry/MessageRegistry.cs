using System;
using Microsoft.Extensions.DependencyInjection;
using Orleans;
using Turbo.Contracts.Abstractions;
using Turbo.Contracts.Enums;
using Turbo.Logging;
using Turbo.Pipeline;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans.Grains;
using Turbo.Primitives.Orleans.Snapshots.Session;

namespace Turbo.Messages.Registry;

public sealed class MessageRegistry(IServiceProvider sp)
    : EnvelopeHost<IMessageEvent, ISessionContext, MessageContext>(
        sp,
        new EnvelopeHostOptions<IMessageEvent, ISessionContext, MessageContext>
        {
            CreateContextAsync = async (env, data) =>
            {
                if (data is null)
                    throw new TurboException(TurboErrorCodeEnum.InvalidSession);

                var sessionKey = data.SessionKey;
                var playerId = (long)-1;
                var roomId = (long)-1;

                var sessionGateway = sp.GetRequiredService<ISessionGateway>();

                if (sessionGateway != null)
                    playerId = sessionGateway.GetPlayerId(sessionKey);

                if (playerId > 0)
                {
                    var grainFactory = sp.GetRequiredService<IGrainFactory>();
                    var playerPresence = grainFactory.GetGrain<IPlayerPresenceGrain>(playerId);
                    var activeRoom = await playerPresence
                        .GetActiveRoomAsync()
                        .ConfigureAwait(false);

                    roomId = activeRoom.RoomId;
                }

                return new MessageContext
                {
                    PlayerId = playerId,
                    RoomId = roomId,
                    Session = data,
                };
            },
            EnableInheritanceDispatch = true,
            HandlerMode = HandlerExecutionMode.Parallel,
            MaxHandlerDegreeOfParallelism = null,
            OnHandlerActivationError = (ex, env) => { },
            OnHandlerInvokeError = (ex, env) => { },
            OnBehaviorActivationError = (ex, env) => { },
            OnBehaviorInvokeError = (ex, env) => { },
        }
    ) { }
