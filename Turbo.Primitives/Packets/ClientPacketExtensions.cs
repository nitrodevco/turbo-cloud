using System;

namespace Turbo.Primitives.Packets;

public static class ClientPacketExtensions
{
    /// <summary>
    /// Reads a collection length the client controls and caps it at what the rest of the packet
    /// could actually hold, so a bogus count cannot make the server allocate or loop for entries
    /// that are not there.
    /// </summary>
    /// <param name="bytesPerItem">
    /// Smallest number of bytes one entry occupies (4 for an int, 2 for a string's length prefix).
    /// </param>
    /// <param name="maxItems">An optional domain limit, applied on top.</param>
    public static int PopCount(
        this IClientPacket packet,
        int bytesPerItem,
        int maxItems = int.MaxValue
    )
    {
        ArgumentNullException.ThrowIfNull(packet);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bytesPerItem);

        var count = packet.PopInt();

        return count <= 0
            ? 0
            : Math.Min(Math.Min(count, packet.Remaining / bytesPerItem), maxItems);
    }
}
