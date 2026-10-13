using Turbo.Primitives.Messages.Outgoing.Help;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Help;

internal class CallForHelpPendingCallsMessageComposerSerializer(int header)
    : AbstractSerializer<CallForHelpPendingCallsMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        CallForHelpPendingCallsMessageComposer message
    )
    {
        packet.WriteInteger(message.Calls.Length);

        foreach (var call in message.Calls)
        {
            packet.WriteString(call.CallId);
            packet.WriteString(call.TimeStamp);
            packet.WriteString(call.Message);
        }
    }
}
