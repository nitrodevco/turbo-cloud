using Turbo.Primitives.Messages.Outgoing.Inventory.Clothing;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Inventory.Clothing;

internal class FigureSetIdsEventMessageComposerSerializer(int header)
    : AbstractSerializer<FigureSetIdsEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        FigureSetIdsEventMessageComposer message
    )
    {
        packet.WriteInteger(message.FigureSetIds.Length);

        foreach (var figureSetId in message.FigureSetIds)
            packet.WriteInteger(figureSetId);

        packet.WriteInteger(message.BoundFurnitureNames.Length);

        foreach (var boundFurnitureName in message.BoundFurnitureNames)
            packet.WriteString(boundFurnitureName);
    }
}
