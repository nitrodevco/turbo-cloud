using Turbo.Primitives.Networking;
using Turbo.Primitives.Plugins.Messages;

namespace Turbo.Messages.PluginMessages;

/// <summary>
/// A plugin message a client sent, as the packet pipeline carries it: one envelope type per
/// message, so its handlers and behaviours are found as a packet's are.
/// </summary>
internal sealed record PluginMessageEvent<T>(T Message) : IMessageEvent
    where T : IPluginMessage;
