using Turbo.Primitives.Messages.Outgoing.Preferences;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Preferences;

internal class DiscordPreferencesMessageComposerSerializer(int header)
    : AbstractSerializer<DiscordPreferencesMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        DiscordPreferencesMessageComposer message
    ) =>
        packet
            .WriteInteger(message.Version)
            .WriteBoolean(message.ShowHabbo)
            .WriteBoolean(message.ShareActivity)
            .WriteBoolean(message.HideInHiddenRooms)
            .WriteBoolean(message.AllowJoining);
}
