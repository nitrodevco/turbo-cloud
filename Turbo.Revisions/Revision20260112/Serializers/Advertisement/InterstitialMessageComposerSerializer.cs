using Turbo.Primitives.Messages.Outgoing.Advertisement;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Advertisement;

internal class InterstitialMessageComposerSerializer(int header)
    : AbstractSerializer<InterstitialMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, InterstitialMessageComposer message)
    {
        //
    }
}
