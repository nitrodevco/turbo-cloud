using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Messages.Outgoing.Room.Furniture;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Room.Furniture;

internal class PresentOpenedMessageComposerSerializer(int header)
    : AbstractSerializer<PresentOpenedMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, PresentOpenedMessageComposer message)
    {
        packet
            .WriteString(message.ItemType)
            .WriteInteger(message.ClassId)
            .WriteString(message.ProductCode.ToLegacyString())
            .WriteInteger(message.PlacedItemId)
            .WriteString(message.PlacedItemType.ToLegacyString())
            .WriteBoolean(message.PlacedInRoom)
            .WriteString(message.PetFigureString);
    }
}
