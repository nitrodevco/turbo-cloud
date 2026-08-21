using System.Collections.Generic;
using Turbo.Primitives.Messages.Incoming.Room.Action;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Players;

namespace Turbo.Revisions.Revision20260701.Parsers.Room.Action;

internal class RemoveRightsMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new RemoveRightsMessage { PlayerIds = ParsePlayerIds(packet) };

    private static List<PlayerId> ParsePlayerIds(IClientPacket packet)
    {
        var count = packet.PopInt();
        var playerIds = new List<PlayerId>();

        for (var i = 0; i < count; i++)
            playerIds.Add(PlayerId.Parse(packet.PopInt()));

        return playerIds;
    }
}
