using Turbo.Primitives.Messages.Incoming.Notifications;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.Notifications;

internal class ResetUnseenItemIdsMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new ResetUnseenItemIdsMessage();
}
