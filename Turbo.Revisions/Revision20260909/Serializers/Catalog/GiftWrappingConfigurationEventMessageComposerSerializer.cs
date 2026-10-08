using System.Collections.Immutable;
using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Catalog;

internal class GiftWrappingConfigurationEventMessageComposerSerializer(int header)
    : AbstractSerializer<GiftWrappingConfigurationEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        GiftWrappingConfigurationEventMessageComposer message
    )
    {
        var wrapping = message.Wrapping;

        packet.WriteBoolean(wrapping.Enabled).WriteInteger(wrapping.Price);

        WriteInts(packet, wrapping.StuffTypes);
        WriteInts(packet, wrapping.BoxTypes);
        WriteInts(packet, wrapping.RibbonTypes);
        WriteInts(packet, wrapping.DefaultStuffTypes);
    }

    private static void WriteInts(IServerPacket packet, ImmutableArray<int> values)
    {
        packet.WriteInteger(values.Length);

        foreach (var value in values)
            packet.WriteInteger(value);
    }
}
