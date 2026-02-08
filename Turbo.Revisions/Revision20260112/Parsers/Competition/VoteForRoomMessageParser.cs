using Turbo.Primitives.Messages.Incoming.Competition;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.Competition;

internal class VoteForRoomMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new VoteForRoomMessage();
}
