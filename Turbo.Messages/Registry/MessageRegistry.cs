using System;
using Turbo.Contracts.Abstractions;
using Turbo.Pipeline;
using Turbo.Primitives.Networking;

namespace Turbo.Messages.Registry;

public sealed class MessageRegistry(IServiceProvider sp)
    : EnvelopeHost<IMessageEvent, ISessionContext, MessageContext>(
        sp,
        new EnvelopeHostOptions<IMessageEvent, ISessionContext, MessageContext>
        {
            CreateContext = (env, data) =>
            {
                ArgumentNullException.ThrowIfNull(data);

                return new MessageContext { Session = data };
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
