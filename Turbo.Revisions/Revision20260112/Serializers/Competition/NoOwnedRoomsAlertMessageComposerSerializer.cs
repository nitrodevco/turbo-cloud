using Turbo.Primitives.Messages.Outgoing.Competition;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Competition;

internal class NoOwnedRoomsAlertMessageComposerSerializer(int header)
    : AbstractSerializer<NoOwnedRoomsAlertMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        NoOwnedRoomsAlertMessageComposer message
    )
    {
        //
    }
}
