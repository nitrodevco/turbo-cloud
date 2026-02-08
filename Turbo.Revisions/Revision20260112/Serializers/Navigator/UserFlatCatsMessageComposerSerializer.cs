using Turbo.Primitives.Messages.Outgoing.Navigator;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Navigator;

internal class UserFlatCatsMessageComposerSerializer(int header)
    : AbstractSerializer<UserFlatCatsMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, UserFlatCatsMessageComposer message)
    {
        //
    }
}
