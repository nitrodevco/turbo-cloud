using Turbo.Contracts.Abstractions;
using Turbo.Pipeline.Registry;

namespace Turbo.Messages.Registry;

public interface IMessageBehavior<in T> : IBehavior<T, MessageContext>
    where T : IMessageEvent;
