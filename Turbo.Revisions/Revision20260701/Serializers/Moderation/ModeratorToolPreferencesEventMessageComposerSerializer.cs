using Turbo.Primitives.Messages.Outgoing.Moderation;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Serializers.Moderation;

internal class ModeratorToolPreferencesEventMessageComposerSerializer(int header)
    : AbstractSerializer<ModeratorToolPreferencesEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        ModeratorToolPreferencesEventMessageComposer message
    )
    {
        //
    }
}
