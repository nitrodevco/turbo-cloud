using Turbo.Primitives.Messages.Incoming.NewNavigator;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.NewNavigator;

internal class NewNavigatorSearchMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new NewNavigatorSearchMessage
        {
            SearchCodeOriginal = packet.PopString(),
            FilteringData = packet.PopString(),
        };
}
