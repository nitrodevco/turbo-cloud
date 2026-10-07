using Turbo.Primitives.Packets;
using Turbo.Primitives.WiredTrading.Snapshots;

namespace Turbo.Revisions.Revision20260909.Parsers.Vault.Data;

/// <summary>Reads a furni type the way the client's <c>ChestItemType.addToComposer</c> writes it.</summary>
internal static class ChestItemTypeParser
{
    public static ChestItemTypeSnapshot Parse(IClientPacket packet)
    {
        var isWallItem = packet.PopBoolean();
        var typeId = packet.PopInt();
        var legacyPosterId = packet.PopString();

        return new ChestItemTypeSnapshot
        {
            IsWallItem = isWallItem,
            TypeId = typeId,
            LegacyPosterId = legacyPosterId,
        };
    }
}
