using Turbo.Primitives.Messages.Outgoing.Roomsettings;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Roomsettings;

internal class ShowEnforceRoomCategoryDialogEventMessageComposerSerializer(int header)
    : AbstractSerializer<ShowEnforceRoomCategoryDialogEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        ShowEnforceRoomCategoryDialogEventMessageComposer message
    )
    {
        //
    }
}
