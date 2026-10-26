using Turbo.Pipeline.Registry;
using Turbo.Primitives.Networking;

namespace Turbo.Messages.Registry;

/// <summary>
/// Handles every <typeparamref name="T"/> a client sends; one in a plugin's assembly is found as
/// the plugin loads. A contract, so it is in <c>Turbo.Primitives</c> with the
/// <see cref="MessageContext"/> it answers through, under the namespace it had in
/// <c>Turbo.Messages</c>.
/// </summary>
public interface IMessageHandler<in T> : IHandler<T, MessageContext>
    where T : IMessageEvent;
