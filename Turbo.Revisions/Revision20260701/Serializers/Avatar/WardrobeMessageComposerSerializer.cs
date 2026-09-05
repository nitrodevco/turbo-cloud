using Turbo.Primitives.Messages.Outgoing.Avatar;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Revisions.Revision20260701.Serializers.Avatar;

internal class WardrobeMessageComposerSerializer(int header)
    : AbstractSerializer<WardrobeMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, WardrobeMessageComposer message)
    {
        packet.WriteInteger(message.State).WriteInteger(message.Outfits.Count);

        foreach (var outfit in message.Outfits)
            packet
                .WriteInteger(outfit.SlotId)
                .WriteString(outfit.Figure)
                .WriteString(AvatarGenderTypeExtensions.ToLegacyString(outfit.Gender));
    }
}
