using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Players;

namespace Turbo.Revisions.Revision20260112.Parsers.Users;

internal class GetExtendedProfileMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new GetExtendedProfileMessage { UserId = (PlayerId)packet.PopInt() };
}
