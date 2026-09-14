using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Serializers.Userdefinedroomevents.Wiredmenu;

internal class WiredErrorLogsEventMessageComposerSerializer(int header)
    : AbstractSerializer<WiredErrorLogsEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        WiredErrorLogsEventMessageComposer message
    )
    {
        packet.WriteInteger(message.Errors.Length);

        foreach (var error in message.Errors)
            packet
                .WriteInteger(error.ErrorId)
                .WriteString(error.ErrorName)
                .WriteString(error.Category)
                .WriteInteger(error.ThrowCount)
                .WriteLong(error.MsSinceLastOccurrence);
    }
}
