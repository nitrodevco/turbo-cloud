using Turbo.Primitives.Messages.Incoming.Navigator;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.Navigator;

internal class CompetitionRoomsSearchMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new CompetitionRoomsSearchMessage { GoalId = packet.PopInt(), PageIndex = packet.PopInt() };
}
