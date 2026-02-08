using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.Users;

internal class GetExtendedProfileByNameMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new GetExtendedProfileByNameMessage { UserName = packet.PopString() };
}
