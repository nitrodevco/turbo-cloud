using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Users;

internal class HabboUserBadgesMessageComposerSerializer(int header)
    : AbstractSerializer<HabboUserBadgesMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, HabboUserBadgesMessageComposer message)
    {
        //
    }
}
