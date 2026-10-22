using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Networking.Capabilities;

namespace Turbo.Primitives.Networking.Extensions;

/// <summary>
/// A plugin's composer as its serializer wrote it (<see cref="IExtensionPacketRegistry"/>), framed
/// and ready for the wire: what a room's stream carries in its place, since the host's serializer
/// knows no plugin's types. Extension packets are the same on every revision, so the bytes are
/// what every session is sent; it still goes only to a session that accepted its
/// <see cref="Capability"/>, or to every session when the plugin's composer names none.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record WrittenExtensionPacket : ICapabilityComposer
{
    /// <summary>The plugin composer's capability, empty when it names none.</summary>
    [Id(0)]
    public required string Capability { get; init; }

    /// <summary>The packet as its serializer framed it: length, header and body.</summary>
    [Id(1)]
    public required ImmutableArray<byte> Bytes { get; init; }

    /// <summary>
    /// <paramref name="composer"/> written by the serializer a plugin registered for its type, or
    /// the composer itself when no plugin did (every core composer).
    /// </summary>
    public static IComposer For(IComposer composer, IExtensionPacketRegistry extensions)
    {
        if (
            composer is WrittenExtensionPacket
            || !extensions.TryGetSerializer(composer.GetType(), out var serializer)
        )
            return composer;

        using var packet = serializer.Serialize(composer);

        return new WrittenExtensionPacket
        {
            Capability = (composer as ICapabilityComposer)?.Capability ?? string.Empty,
            Bytes = ImmutableArray.Create(packet.ToArray()),
        };
    }

    /// <summary><see cref="For(IComposer, IExtensionPacketRegistry)"/> for each of a batch, in order.</summary>
    public static ImmutableArray<IComposer> For(
        ImmutableArray<IComposer> composers,
        IExtensionPacketRegistry extensions
    )
    {
        var written = composers;

        for (var i = 0; i < composers.Length; i++)
        {
            var composer = For(composers[i], extensions);

            if (!ReferenceEquals(composer, composers[i]))
                written = written.SetItem(i, composer);
        }

        return written;
    }
}
