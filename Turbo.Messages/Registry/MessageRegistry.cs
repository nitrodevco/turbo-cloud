using System;
using Microsoft.Extensions.DependencyInjection;
using Orleans;
using Turbo.Logging;
using Turbo.Pipeline;
using Turbo.Primitives;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players.Grains;

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

                var grainFactory = sp.GetRequiredService<IGrainFactory>();
                var sessionGateway = sp.GetRequiredService<ISessionGateway>();
                var playerId = sessionGateway.GetPlayerId(data.SessionKey);
                var roomId = (long)-1;

                if (playerId > 0)
                {
                    var playerPresence = grainFactory.GetGrain<IPlayerPresenceGrain>(playerId);
                    var activeRoom = await playerPresence
                        .GetActiveRoomAsync()
                        .ConfigureAwait(false);

                    roomId = activeRoom.RoomId.Value;
                }

                return new(data, playerId, roomId);
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
