using System.Collections.Generic;
using Turbo.Primitives.Messages.Incoming.Room.Action;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.Room.Action;

internal class RemoveRightsMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new RemoveRightsMessage { UserIds = ParseUserIds(packet) };

    private static List<int> ParseUserIds(IClientPacket packet)
    {
        var count = packet.PopInt();
        var userIds = new List<int>();

        for (var i = 0; i < count; i++)
            userIds.Add(packet.PopInt());

        return userIds;
    }
}
