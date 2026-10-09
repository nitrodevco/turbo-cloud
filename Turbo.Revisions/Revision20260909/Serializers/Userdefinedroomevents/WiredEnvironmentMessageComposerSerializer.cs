using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Userdefinedroomevents;

internal class WiredEnvironmentMessageComposerSerializer(int header)
    : AbstractSerializer<WiredEnvironmentMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, WiredEnvironmentMessageComposer message)
    {
        packet.WriteBoolean(message.HasClickUserWired);
        packet.WriteInteger(message.EnabledAchievements.Length);

        foreach (var name in message.EnabledAchievements)
            packet.WriteString(name);
    }
}
