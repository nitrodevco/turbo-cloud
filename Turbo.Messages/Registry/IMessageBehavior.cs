using Turbo.Contracts.Abstractions;
using Turbo.Pipeline.Abstractions.Registry;

namespace Turbo.Messages.Registry;

public interface IMessageBehavior<in T> : IBehavior<T, MessageContext>
    where T : IMessageEvent;
