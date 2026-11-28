using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260909.Serializers.Pets.Data;

namespace Turbo.Revisions.Revision20260909.Serializers.Users;

/// <summary>
/// A pet was respected. <paramref name="withLocations"/> is the layout a client with
/// <c>pet_inventory_locations</c> reads, whose pet ends with the room it was respected in.
/// </summary>
internal class PetRespectNotificationEventMessageComposerSerializer(
    int header,
    bool withLocations = false
) : AbstractSerializer<PetRespectNotificationEventMessageComposer>(header)
{
    private readonly bool _withLocations = withLocations;

    protected override void Serialize(
        IServerPacket packet,
        PetRespectNotificationEventMessageComposer message
    )
    {
        packet.WriteInteger(message.Respect).WriteInteger(message.PetOwnerId);

        if (_withLocations)
            PetDataSerializer.SerializeWithLocation(packet, message.Pet, message.Room);
        else
            PetDataSerializer.Serialize(packet, message.Pet);
    }
}
