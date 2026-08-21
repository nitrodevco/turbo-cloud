using Turbo.Primitives.Messages.Incoming.Competition;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Parsers.Competition;

internal class RoomCompetitionInitMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new RoomCompetitionInitMessage();
}
