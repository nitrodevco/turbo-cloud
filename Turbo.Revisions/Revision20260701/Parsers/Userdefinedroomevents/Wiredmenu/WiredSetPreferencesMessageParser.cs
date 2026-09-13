using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Parsers.Userdefinedroomevents.Wiredmenu;

internal class WiredSetPreferencesMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new WiredSetPreferencesMessage
        {
            WiredMenuButton = packet.PopBoolean(),
            WiredInspectButton = packet.PopBoolean(),
            WiredPlayTestMode = packet.PopBoolean(),
            VariableSyntaxMode = packet.PopInt(),
            WiredWhisperDisabled = packet.PopBoolean(),
            ShowAllNotifications = packet.PopBoolean(),
            UIStyle = packet.PopString(),
        };
}
