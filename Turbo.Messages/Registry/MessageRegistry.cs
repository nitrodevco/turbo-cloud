using System;
using Turbo.Contracts.Abstractions;
using Turbo.Networking.Abstractions.Session;
using Turbo.Pipeline;

namespace Turbo.Messages.Registry;

public sealed class MessageRegistry : EnvelopeHost<IMessageEvent, ISessionContext, MessageContext>
{
    public MessageRegistry()
        : base(
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
}
