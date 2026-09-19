using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Userdefinedroomevents.Wiredmenu;

internal class WiredDeleteAllVariableHoldersMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new WiredDeleteAllVariableHoldersMessage { SelectedVariableId = packet.PopString() };
}
