using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260909.Serializers.Userdefinedroomevents.Data;

namespace Turbo.Revisions.Revision20260909.Serializers.Userdefinedroomevents;

/// <summary>Flash <c>FurnitureLabelsAndRegionsMessageParser</c>: int count, then each entry.</summary>
internal class FurnitureLabelsAndRegionsMessageComposerSerializer(int header)
    : AbstractSerializer<FurnitureLabelsAndRegionsMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        FurnitureLabelsAndRegionsMessageComposer message
    )
    {
        packet.WriteInteger(message.Entries.Length);

        foreach (var entry in message.Entries)
            FurnitureLabelSerializer.Serialize(packet, entry);
    }
}
