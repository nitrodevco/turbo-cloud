using Turbo.Primitives.Messages.Outgoing.Moderation;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Moderation;

internal class ModeratorToolPreferencesEventMessageComposerSerializer(int header)
    : AbstractSerializer<ModeratorToolPreferencesEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        ModeratorToolPreferencesEventMessageComposer message
    )
    {
        packet
            .WriteInteger(message.Preferences.WindowX)
            .WriteInteger(message.Preferences.WindowY)
            .WriteInteger(message.Preferences.WindowWidth)
            .WriteInteger(message.Preferences.WindowHeight);
    }
}
