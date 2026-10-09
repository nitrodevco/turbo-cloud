using Turbo.Primitives.Messages.Outgoing.Callforhelp;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Callforhelp;

/// <summary>
/// <c>CfhTopicsInitMessageParser</c>: a count of categories, each its name and a count of
/// topics, each topic its name, id and consequence.
/// </summary>
internal class CfhTopicsInitMessageComposerSerializer(int header)
    : AbstractSerializer<CfhTopicsInitMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, CfhTopicsInitMessageComposer message)
    {
        packet.WriteInteger(message.Categories.Length);

        foreach (var category in message.Categories)
        {
            packet.WriteString(category.Name).WriteInteger(category.Topics.Length);

            foreach (var topic in category.Topics)
                packet
                    .WriteString(topic.Name)
                    .WriteInteger(topic.Id)
                    .WriteString(topic.Consequence);
        }
    }
}
