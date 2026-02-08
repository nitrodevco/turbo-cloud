using Turbo.Primitives.Messages.Incoming.NewNavigator;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.NewNavigator;

internal class NavigatorAddSavedSearchMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new NavigatorAddSavedSearchMessage
        {
            SearchCode = packet.PopString(),
            Filter = packet.PopString(),
        };
}
