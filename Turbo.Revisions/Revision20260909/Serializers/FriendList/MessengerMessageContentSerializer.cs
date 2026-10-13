using Turbo.Primitives.Packets;
using Turbo.Primitives.Players.Enums.Messenger;

namespace Turbo.Revisions.Revision20260909.Serializers.FriendList;

/// <summary>
/// What a console message carries, as the client's content reader takes it
/// (<c>incoming/friendlist/_-W1u.parse</c>): type 0 and the text, or type 1 and the Habbicon.
/// </summary>
internal static class MessengerMessageContentSerializer
{
    public static void Serialize(IServerPacket packet, string text, int habbiconId)
    {
        if (habbiconId > 0)
        {
            packet.WriteInteger((int)MessengerMessageContentType.Habbicon);
            packet.WriteInteger(habbiconId);

            return;
        }

        packet.WriteInteger((int)MessengerMessageContentType.Text);
        packet.WriteString(text);
    }
}
