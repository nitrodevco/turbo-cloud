using System;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Networking.Capabilities;

namespace Turbo.Primitives.Plugins.Messages;

/// <summary>
/// A plugin message that carries a whole packet of the hotel's after its own fields: another
/// room's furni or avatars relayed to a player, say. The message declares it as its last field,
/// an <see cref="IComposer"/> (schema type <c>"packet"</c>, <see cref="PluginMessageSchema.CarriesPacket"/>).
/// On the wire it is the plugin message (<see cref="Outer"/>: its name's header, then the fields
/// before the packet) followed by <see cref="Inner"/> as the session's encoder writes it, its
/// short header and body without the length. A client reads the carried field with its own parser
/// for that header. The inner packet is serialized once for every session it goes to, as a room's
/// broadcast is; only the envelope's frame is written per session. Like any plugin message it
/// reaches only a session that accepted <c>client.messages</c>.
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

    /// <summary><paramref name="message"/> with the packet its last field carries.</summary>
    /// <exception cref="ArgumentException">
    /// The message carries no packet field, its packet is null, or the packet is an envelope.
    /// </exception>
    public static PluginEnvelopeComposer For(IPluginMessage message)
    {
        var schema = CarryingSchema(message);

        return For(
            PluginMessageComposer.Fields(schema, message),
            schema.CarriedOf(message)
                ?? throw new ArgumentException(
                    $"{schema.Name} carries no packet: its last field is null.",
                    nameof(message)
                )
        );
    }

    /// <summary>
    /// <paramref name="message"/> carrying <paramref name="inner"/>, whatever its own packet field
    /// holds.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The message carries no packet field, or <paramref name="inner"/> is an envelope.
    /// </exception>
    public static PluginEnvelopeComposer For(IPluginMessage message, IComposer inner) =>
        For(OuterOf(message), inner);

    /// <summary><paramref name="inner"/> inside an outer message already written.</summary>
    /// <exception cref="ArgumentException"><paramref name="inner"/> is an envelope.</exception>
    public static PluginEnvelopeComposer For(PluginMessageComposer outer, IComposer inner) =>
        inner is PluginEnvelopeComposer
            ? throw new ArgumentException(
                "An envelope cannot carry another envelope.",
                nameof(inner)
            )
            : new() { Outer = outer, Inner = inner };

    /// <summary>
    /// The fields of <paramref name="message"/> before its carried packet, written once: what a
    /// room watch wraps each of the room's packets in.
    /// </summary>
    /// <exception cref="ArgumentException">The message carries no packet field.</exception>
    public static PluginMessageComposer OuterOf(IPluginMessage message) =>
        PluginMessageComposer.Fields(CarryingSchema(message), message);

    private static PluginMessageSchema CarryingSchema(IPluginMessage message)
    {
        var schema = PluginMessageSchema.For(message.GetType());

        return schema.CarriesPacket
            ? schema
            : throw new ArgumentException(
                $"{schema.Name} has no carried packet: give it a last field of type IComposer.",
                nameof(message)
            );
    }
}
