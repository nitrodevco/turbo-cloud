using Turbo.Primitives.Messages.Outgoing.Campaign;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Campaign;

/// <summary>Opened, product name, custom image, furni class name (AS3 CampaignCalendarDoorOpenedMessageParser).</summary>
internal class CampaignCalendarDoorOpenedMessageComposerSerializer(int header)
    : AbstractSerializer<CampaignCalendarDoorOpenedMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        CampaignCalendarDoorOpenedMessageComposer message
    )
    {
        packet.WriteBoolean(message.DoorOpened);
        packet.WriteString(message.ProductName);
        packet.WriteString(message.CustomImage);
        packet.WriteString(message.FurnitureClassName);
    }
}
