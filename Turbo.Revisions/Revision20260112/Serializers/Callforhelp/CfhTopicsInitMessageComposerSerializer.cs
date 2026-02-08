using Turbo.Primitives.Messages.Outgoing.Callforhelp;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Callforhelp;

internal class CfhTopicsInitMessageComposerSerializer(int header)
    : AbstractSerializer<CfhTopicsInitMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, CfhTopicsInitMessageComposer message)
    {
        //
    }
}
