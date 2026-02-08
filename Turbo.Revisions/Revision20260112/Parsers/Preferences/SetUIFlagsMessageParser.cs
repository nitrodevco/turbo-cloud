using Turbo.Primitives.Messages.Incoming.Preferences;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.Preferences;

internal class SetUIFlagsMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new SetUIFlagsMessage();
}
