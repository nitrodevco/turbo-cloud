using Turbo.Pipeline.Registry;
using Turbo.Primitives.Networking;

namespace Turbo.Messages.Registry;

public interface IMessageBehavior<in T> : IBehavior<T, MessageContext>
    where T : IMessageEvent;
