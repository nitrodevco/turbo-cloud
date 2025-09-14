using Turbo.Contracts.Abstractions;
using Turbo.Pipeline.Registry;

namespace Turbo.Messages.Registry;

public interface IMessageHandler<in T> : IHandler<T, MessageContext>
    where T : IMessageEvent;
