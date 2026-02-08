using Turbo.Primitives.Messages.Outgoing.Roomsettings;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Roomsettings;

internal class RoomSettingsSaveErrorEventMessageComposerSerializer(int header)
    : AbstractSerializer<RoomSettingsSaveErrorEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        RoomSettingsSaveErrorEventMessageComposer message
    )
    {
        //
    }
}
