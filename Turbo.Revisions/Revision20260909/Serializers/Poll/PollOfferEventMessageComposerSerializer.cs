using Turbo.Primitives.Messages.Outgoing.Poll;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Poll;

/// <summary>Flash's PollOfferParser.as:43: id, type, headline, summary.</summary>
internal class PollOfferEventMessageComposerSerializer(int header)
    : AbstractSerializer<PollOfferEventMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, PollOfferEventMessageComposer message)
    {
        packet
            .WriteInteger(message.PollId)
            .WriteString(message.PollType)
            .WriteString(message.Headline)
            .WriteString(message.Summary);
    }
}
