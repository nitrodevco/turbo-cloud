using System.Collections.Immutable;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Players;

namespace Turbo.Revisions.Revision20260909.Serializers.Room.Engine.Data;

/// <summary>The owner id → name table that heads both the floor and the wall item lists.</summary>
internal static class OwnerNamesSerializer
{
    public static void Serialize(
        IServerPacket packet,
        ImmutableDictionary<PlayerId, string> ownerNames
    )
    {
        packet.WriteInteger(ownerNames.Count);

        foreach (var (ownerId, ownerName) in ownerNames)
            packet.WriteInteger(ownerId).WriteString(ownerName);
    }
}
