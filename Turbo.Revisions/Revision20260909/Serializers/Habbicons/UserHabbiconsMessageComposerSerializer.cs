using Turbo.Primitives.Messages.Outgoing.Habbicons;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Habbicons;

/// <summary>The owned Habbicons, then the recent ids (the client's <c>_-A2t</c>).</summary>
internal class UserHabbiconsMessageComposerSerializer(int header)
    : AbstractSerializer<UserHabbiconsMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, UserHabbiconsMessageComposer message)
    {
        packet.WriteInteger(message.Habbicons.Length);

        foreach (var habbicon in message.Habbicons)
            packet.WriteInteger(habbicon.HabbiconId).WriteInteger((int)habbicon.State);

        packet.WriteInteger(message.RecentHabbiconIds.Length);

        foreach (var habbiconId in message.RecentHabbiconIds)
            packet.WriteInteger(habbiconId);
    }
}
