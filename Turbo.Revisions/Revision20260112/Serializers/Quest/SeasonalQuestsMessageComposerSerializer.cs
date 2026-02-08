using Turbo.Primitives.Messages.Outgoing.Quest;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Quest;

internal class SeasonalQuestsMessageComposerSerializer(int header)
    : AbstractSerializer<SeasonalQuestsMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, SeasonalQuestsMessageComposer message)
    {
        //
    }
}
