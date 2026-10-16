using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Userdefinedroomevents.Wiredmenu;

/// <summary>Flash <c>WiredMenuErrorMessageParser.parse</c>: <c>readShort</c>.</summary>
internal class WiredMenuErrorEventMessageComposerSerializer(int header)
    : AbstractSerializer<WiredMenuErrorEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        WiredMenuErrorEventMessageComposer message
    )
    {
        packet.WriteShort(message.ErrorCode);
    }
}
