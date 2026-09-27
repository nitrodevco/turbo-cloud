using System.Runtime.CompilerServices;
using System.Threading;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Networking.Package;

/// <summary>
/// The framed, unencrypted bytes of recently serialized composers, keyed by the composer
/// instance and the serializer that wrote them, so one revision's bytes never answer for
/// another's.
///
/// A room broadcast reaches every recipient's session as the same composer instance: the room
/// stream hands one deserialized batch to every subscriber on the silo, and composers are
/// <c>[Immutable]</c>, so Orleans does not copy them on the local calls to the presence grains
/// and their session observers. Without this each broadcast was serialized once per recipient.
/// Encryption is per session and ordering-sensitive, so it happens on the copy the encoder
/// writes to the transport, never on the bytes held here.
///
/// Direct-mapped and lossy on purpose: a slot holds the last composer that hashed to it, a
/// collision only costs a second serialization, and what is held is bounded by the slot count
/// times the largest payload kept (8 MB at most, far less in practice).
/// </summary>
public sealed class ComposerPayloadCache
{
    private const int SLOT_COUNT = 512;

    // Broadcasts are small (movement, chat, furni updates). The large packets (inventories,
    // catalog pages, room entry) go to one player, so keeping them would only pin memory.
    private const int MAX_CACHED_PAYLOAD_BYTES = 16 * 1024;

    private readonly Entry?[] _slots = new Entry?[SLOT_COUNT];

    /// <summary>
    /// The composer's bytes as <paramref name="serializer"/> frames them. The array is shared
    /// between sessions and must not be written to.
    /// </summary>
    public byte[] GetOrSerialize(ISerializer serializer, IComposer composer)
    {
        var slot = RuntimeHelpers.GetHashCode(composer) & (SLOT_COUNT - 1);
        var entry = Volatile.Read(ref _slots[slot]);

        if (
            entry is not null
            && ReferenceEquals(entry.Composer, composer)
            && ReferenceEquals(entry.Serializer, serializer)
        )
            return entry.Payload;

        byte[] payload;

        using (var packet = serializer.Serialize(composer))
            payload = packet.ToArray();

        if (payload.Length <= MAX_CACHED_PAYLOAD_BYTES)
            Volatile.Write(ref _slots[slot], new Entry(composer, serializer, payload));

        return payload;
    }

    private sealed class Entry(IComposer composer, ISerializer serializer, byte[] payload)
    {
        public IComposer Composer { get; } = composer;
        public ISerializer Serializer { get; } = serializer;
        public byte[] Payload { get; } = payload;
    }
}
