using Turbo.Pipeline.Registry;
using Turbo.Primitives.Networking;

namespace Turbo.Messages.Registry;

public interface IMessageHandler<in T> : IHandler<T, MessageContext>
    where T : IMessageEvent;
