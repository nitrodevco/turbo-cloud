using Turbo.Primitives.Messages.Incoming.Preferences;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.Preferences;

internal class SetChatPreferencesMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new SetChatPreferencesMessage();
}
