using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Networking.Capabilities;
using Turbo.Primitives.Packets;

namespace Turbo.Primitives.Plugins.Messages;

/// <summary>
/// A plugin message on its way to clients: its name and its fields already written by its schema,
/// so it crosses grains and room streams as plain bytes whatever the plugin's record is. The
/// session's encoder frames it with the header the host gave the name. It goes only to a session
/// that accepted <c>client.messages</c>, the clients that know the headers.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record PluginMessageComposer : ICapabilityComposer
{
    [Id(0)]
    public required string Name { get; init; }

    /// <summary>The fields as the schema wrote them, without a header or length.</summary>
    [Id(1)]
    public required ImmutableArray<byte> Body { get; init; }

    public string Capability => ClientCapabilities.CLIENT_MESSAGES;

    /// <summary><paramref name="message"/>, written by its schema.</summary>
    /// <exception cref="System.ArgumentException">The message's type is not a valid plugin message.</exception>
    public static PluginMessageComposer For(IPluginMessage message)
    {
        var schema = PluginMessageSchema.For(message.GetType());
        using var packet = new ServerPacket(0);

        schema.Write(packet, message);

        return new PluginMessageComposer
        {
            Name = schema.Name,
            Body = ImmutableArray.Create(packet.WrittenSpan),
        };
    }
}
