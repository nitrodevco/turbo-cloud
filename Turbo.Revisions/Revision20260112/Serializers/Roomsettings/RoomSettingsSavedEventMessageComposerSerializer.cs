using Turbo.Primitives.Messages.Outgoing.Roomsettings;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Roomsettings;

internal class RoomSettingsSavedEventMessageComposerSerializer(int header)
    : AbstractSerializer<RoomSettingsSavedEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        RoomSettingsSavedEventMessageComposer message
    )
    {
        //
    }
}
