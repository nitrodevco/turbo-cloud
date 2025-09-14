using Turbo.Contracts.Abstractions;
using Turbo.Pipeline.Abstractions.Registry;

namespace Turbo.Messages.Registry;

public interface IMessageHandler<in T> : IHandler<T, MessageContext>
    where T : IMessageEvent;
