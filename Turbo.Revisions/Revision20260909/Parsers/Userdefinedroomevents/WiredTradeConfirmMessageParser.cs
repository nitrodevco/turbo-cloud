using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Userdefinedroomevents;

internal class WiredTradeConfirmMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet)
    {
        var isFinalConfirm = packet.PopBoolean();

        return new WiredTradeConfirmMessage { IsFinalConfirm = isFinalConfirm };
    }
}
