using Turbo.Primitives.Messages.Outgoing.Landingview;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Landingview;

internal class PromoArticlesMessageComposerSerializer(int header)
    : AbstractSerializer<PromoArticlesMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, PromoArticlesMessageComposer message)
    {
        packet.WriteInteger(message.Articles.Length);

        foreach (var article in message.Articles)
        {
            packet.WriteInteger(article.Id);
            packet.WriteString(article.Title);
            packet.WriteString(article.BodyText);
            packet.WriteString(article.ButtonText);
            packet.WriteInteger((int)article.LinkType);
            packet.WriteString(article.LinkContent);
            packet.WriteString(article.ImageUrl);
        }
    }
}
