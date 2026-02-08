using Turbo.Primitives.Messages.Outgoing.Roomsettings;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Roomsettings;

internal class RoomSettingsDataEventMessageComposerSerializer(int header)
    : AbstractSerializer<RoomSettingsDataEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        RoomSettingsDataEventMessageComposer message
    )
    {
        //
    }
}
