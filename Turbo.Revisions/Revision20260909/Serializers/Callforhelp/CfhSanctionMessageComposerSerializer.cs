using Turbo.Primitives.Messages.Outgoing.Callforhelp;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Callforhelp;

internal class CfhSanctionMessageComposerSerializer(int header)
    : AbstractSerializer<CfhSanctionMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, CfhSanctionMessageComposer message)
    {
        packet.WriteInteger(message.IssueId);
        packet.WriteInteger(message.AccountId);
        packet.WriteString(message.Sanction.Name);
        packet.WriteInteger(message.Sanction.LengthHours);
        packet.WriteInteger(message.Sanction.Unknown);
        // Not avatar only; no trade lock or machine ban with it.
        packet.WriteBoolean(false);
        packet.WriteString(string.Empty);
        packet.WriteString(string.Empty);
    }
}
