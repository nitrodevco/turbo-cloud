using Turbo.Primitives.Messages.Outgoing.Help;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Help;

/// <summary><c>CallForHelpResultMessageParser</c>: the result type, then the text shown.</summary>
internal class CallForHelpResultMessageComposerSerializer(int header)
    : AbstractSerializer<CallForHelpResultMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        CallForHelpResultMessageComposer message
    )
    {
        packet.WriteInteger((int)message.Result.Result).WriteString(message.Result.Message);
    }
}
