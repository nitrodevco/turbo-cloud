using Turbo.Primitives.Messages.Outgoing.Callforhelp;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Callforhelp;

internal class CfhSanctionMessageComposerSerializer(int header)
    : AbstractSerializer<CfhSanctionMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, CfhSanctionMessageComposer message)
    {
        //
    }
}
