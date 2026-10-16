using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Userdefinedroomevents;

internal class WiredWebApiKeyResultMessageComposerSerializer(int header)
    : AbstractSerializer<WiredWebApiKeyResultMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        WiredWebApiKeyResultMessageComposer message
    )
    {
        packet
            .WriteInteger(message.WiredId)
            .WriteBoolean(message.IsReadKey)
            .WriteString(message.Key);
    }
}
