using Turbo.Primitives.Messages.Outgoing.Callforhelp;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Callforhelp;

internal class SanctionStatusEventMessageComposerSerializer(int header)
    : AbstractSerializer<SanctionStatusEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        SanctionStatusEventMessageComposer message
    )
    {
        //
    }
}
