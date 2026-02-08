using Turbo.Primitives.Messages.Incoming.NewNavigator;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.NewNavigator;

internal class NavigatorAddCollapsedCategoryMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new NavigatorAddCollapsedCategoryMessage { CategoryName = packet.PopString() };
}
