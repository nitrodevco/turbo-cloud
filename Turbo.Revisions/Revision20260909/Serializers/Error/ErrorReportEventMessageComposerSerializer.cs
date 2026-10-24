using System.Globalization;
using Turbo.Primitives.Messages.Outgoing.Error;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Error;

internal class ErrorReportEventMessageComposerSerializer(int header)
    : AbstractSerializer<ErrorReportEventMessageComposer>(header)
{
    // ErrorReportMessageParser reads the message id, the code, then the time (official client
    // capture 2026-10-24: "10/24/26, 7:40 AM").
    protected override void Serialize(IServerPacket packet, ErrorReportEventMessageComposer message)
    {
        packet
            .WriteInteger(message.MessageId)
            .WriteInteger(message.ErrorCode)
            .WriteString(
                message.Time.UtcDateTime.ToString("M/d/yy, h:mm tt", CultureInfo.InvariantCulture)
            );
    }
}
