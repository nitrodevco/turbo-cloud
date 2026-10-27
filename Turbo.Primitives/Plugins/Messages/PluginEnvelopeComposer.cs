using System;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Networking.Capabilities;

namespace Turbo.Primitives.Plugins.Messages;

/// <summary>
/// A plugin message that carries a whole packet of the hotel's after its own fields: another
/// room's furni or avatars relayed to a player, say. On the wire it is the plugin message
/// (<see cref="Outer"/>: its name's header, then its fields) followed by <see cref="Inner"/> as the
/// session's encoder writes it, its short header and body without the length. A client module
/// reads its own fields and hands the rest to <c>api.packets.parseCore</c>. The inner packet is
/// serialized once for every session it goes to, as a room's broadcast is; only the envelope's
/// frame is written per session. Like any plugin message it reaches only a session that
/// accepted <c>client.messages</c>.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record PluginEnvelopeComposer : ICapabilityComposer
{
    /// <summary>The plugin message that frames the packet: its name and its own fields.</summary>
    [Id(0)]
    public required PluginMessageComposer Outer { get; init; }

    /// <summary>
    /// The packet carried: a core composer, a plugin's composer or a plugin message, never
    /// another envelope.
    /// </summary>
    [Id(1)]
    public required IComposer Inner { get; init; }

    public string Capability => ClientCapabilities.CLIENT_MESSAGES;

    /// <summary><paramref name="inner"/> inside <paramref name="outer"/>, written by its schema.</summary>
    /// <exception cref="ArgumentException"><paramref name="inner"/> is an envelope.</exception>
    public static PluginEnvelopeComposer For(IPluginMessage outer, IComposer inner) =>
        For(PluginMessageComposer.For(outer), inner);

    /// <summary><paramref name="inner"/> inside an outer message already written.</summary>
    /// <exception cref="ArgumentException"><paramref name="inner"/> is an envelope.</exception>
    public static PluginEnvelopeComposer For(PluginMessageComposer outer, IComposer inner) =>
        inner is PluginEnvelopeComposer
            ? throw new ArgumentException(
                "An envelope cannot carry another envelope.",
                nameof(inner)
            )
            : new() { Outer = outer, Inner = inner };
}
