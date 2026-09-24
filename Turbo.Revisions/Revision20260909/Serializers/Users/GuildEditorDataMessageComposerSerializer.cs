using System.Collections.Immutable;
using Turbo.Primitives.Guilds.Snapshots;
using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Users;

internal class GuildEditorDataMessageComposerSerializer(int header)
    : AbstractSerializer<GuildEditorDataMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, GuildEditorDataMessageComposer message)
    {
        var data = message.EditorData;

        SerializeParts(packet, data.BaseParts);
        SerializeParts(packet, data.SymbolParts);
        SerializeColors(packet, data.BadgeColors);
        SerializeColors(packet, data.PrimaryColors);
        SerializeColors(packet, data.SecondaryColors);
    }

    private static void SerializeParts(
        IServerPacket packet,
        ImmutableArray<GuildBadgePartDefinitionSnapshot> parts
    )
    {
        packet.WriteInteger(parts.Length);

        foreach (var part in parts)
            packet
                .WriteInteger(part.PartId)
                .WriteString(part.FileName)
                .WriteString(part.MaskFileName);
    }

    /// <summary>The client parses the colour as hexadecimal, so it travels as text.</summary>
    private static void SerializeColors(
        IServerPacket packet,
        ImmutableArray<GuildColorSnapshot> colors
    )
    {
        packet.WriteInteger(colors.Length);

        foreach (var color in colors)
            packet.WriteInteger(color.ColorId).WriteString(color.Color);
    }
}
