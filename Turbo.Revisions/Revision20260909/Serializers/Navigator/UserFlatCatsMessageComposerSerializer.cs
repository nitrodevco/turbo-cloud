using Turbo.Primitives.Messages.Outgoing.Navigator;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Navigator;

internal class UserFlatCatsMessageComposerSerializer(int header)
    : AbstractSerializer<UserFlatCatsMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, UserFlatCatsMessageComposer message)
    {
        packet.WriteInteger(message.Categories.Length);

        foreach (var category in message.Categories)
        {
            packet
                .WriteInteger(category.Id)
                .WriteString(category.Name)
                .WriteBoolean(category.Visible)
                .WriteBoolean(category.Automatic)
                .WriteString(category.AutomaticCategoryKey)
                .WriteString(category.GlobalCategoryKey)
                .WriteBoolean(category.StaffOnly);
        }
    }
}
