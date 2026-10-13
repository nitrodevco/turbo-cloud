using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260909.Serializers.Userdefinedroomevents.Data;

namespace Turbo.Revisions.Revision20260909.Serializers.Userdefinedroomevents;

/// <summary>
/// Flash <c>FurnitureLabelsAndRegionsUpdateMessageParser</c>: int count, each added or changed
/// entry, then int count and each furni id whose label or region is gone.
/// </summary>
internal class FurnitureLabelsAndRegionsUpdateMessageComposerSerializer(int header)
    : AbstractSerializer<FurnitureLabelsAndRegionsUpdateMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        FurnitureLabelsAndRegionsUpdateMessageComposer message
    )
    {
        packet.WriteInteger(message.AddedOrUpdated.Length);

        foreach (var entry in message.AddedOrUpdated)
            FurnitureLabelSerializer.Serialize(packet, entry);

        packet.WriteInteger(message.RemovedIds.Length);

        foreach (var objectId in message.RemovedIds)
            packet.WriteInteger(objectId);
    }
}
