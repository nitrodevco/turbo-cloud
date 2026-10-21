using System;
using System.Linq;
using Turbo.Primitives.Messages.Outgoing.Turbo;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Turbo;

internal class TurboNoticeMessageSerializer(int header)
    : AbstractSerializer<TurboNoticeMessage>(header)
{
    protected override void Serialize(IServerPacket packet, TurboNoticeMessage message)
    {
        packet.WriteString(message.Key);
        packet.WriteBoolean(message.Shown);
        packet.WriteInteger(message.Variables.Count);

        foreach (var (name, value) in message.Variables.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            packet.WriteString(name);
            packet.WriteString(value);
        }

        packet.WriteString(message.Description);
    }
}
