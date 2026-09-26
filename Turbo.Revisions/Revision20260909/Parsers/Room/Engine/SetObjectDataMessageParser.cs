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
        // Bounded as strings first, so a bogus count cannot size the dictionary.
        var pairs = packet.PopCount(bytesPerItem: 2) / 2;
        var data = new Dictionary<string, string>(pairs);

        for (var i = 0; i < pairs; i++)
            data[packet.PopString()] = packet.PopString();

        return new SetObjectDataMessage { ObjectId = objectId, Data = data };
    }
}
