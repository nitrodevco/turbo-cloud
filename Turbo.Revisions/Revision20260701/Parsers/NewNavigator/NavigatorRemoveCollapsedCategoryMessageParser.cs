using Turbo.Primitives.Messages.Incoming.NewNavigator;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Parsers.NewNavigator;

internal class NavigatorRemoveCollapsedCategoryMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new NavigatorRemoveCollapsedCategoryMessage { CategoryName = packet.PopString() };
}
