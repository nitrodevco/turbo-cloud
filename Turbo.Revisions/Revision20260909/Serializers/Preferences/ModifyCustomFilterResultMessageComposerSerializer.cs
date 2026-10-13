using Turbo.Primitives.Messages.Outgoing.Preferences;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Preferences;

internal class ModifyCustomFilterResultMessageComposerSerializer(int header)
    : AbstractSerializer<ModifyCustomFilterResultMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        ModifyCustomFilterResultMessageComposer message
    )
    {
        packet.WriteInteger(message.Result).WriteString(message.Word);
    }
}
