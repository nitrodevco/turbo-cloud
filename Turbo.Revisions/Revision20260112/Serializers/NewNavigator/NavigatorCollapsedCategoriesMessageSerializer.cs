using Turbo.Primitives.Messages.Outgoing.NewNavigator;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.NewNavigator;

internal class NavigatorCollapsedCategoriesMessageSerializer(int header)
    : AbstractSerializer<NavigatorCollapsedCategoriesMessage>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        NavigatorCollapsedCategoriesMessage message
    )
    {
        packet.WriteInteger(message.CollapsedCategoryIds.Count);

        foreach (var categoryId in message.CollapsedCategoryIds)
        {
            packet.WriteString(categoryId);
        }
    }
}
