using System;
using System.Collections.Generic;
using Turbo.Primitives.Messages.Incoming.Room.Engine;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Room.Engine;

internal class SetObjectDataMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet)
    {
        var objectId = packet.PopInt();
        // The client writes the entry count doubled: one string for the key, one for the value.
        var pairs = packet.PopInt() / 2;
        var data = new Dictionary<string, string>(Math.Max(0, pairs));

        for (var i = 0; i < pairs; i++)
            data[packet.PopString()] = packet.PopString();

        return new SetObjectDataMessage { ObjectId = objectId, Data = data };
    }
}
