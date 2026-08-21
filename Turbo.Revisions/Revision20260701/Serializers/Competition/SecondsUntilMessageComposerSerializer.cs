using Turbo.Primitives.Messages.Outgoing.Competition;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Serializers.Competition;

internal class SecondsUntilMessageComposerSerializer(int header)
    : AbstractSerializer<SecondsUntilMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, SecondsUntilMessageComposer message)
    {
        //
    }
}
