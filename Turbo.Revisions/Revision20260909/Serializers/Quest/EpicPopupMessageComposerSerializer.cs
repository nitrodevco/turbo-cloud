using Turbo.Primitives.Messages.Outgoing.Quest;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Quest;

/// <summary>Read by EpicPopupMessageParser.as:15-17 (WIN63-202610151745): one string.</summary>
internal class EpicPopupMessageComposerSerializer(int header)
    : AbstractSerializer<EpicPopupMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, EpicPopupMessageComposer message)
    {
        packet.WriteString(message.ImageUri);
    }
}
