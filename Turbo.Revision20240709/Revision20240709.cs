using System;
using System.Collections.Generic;
using Turbo.Core.Packets.Messages;
using Turbo.Core.Packets.Revisions;
using Turbo.Packets.Outgoing.Handshake;
using Turbo.Revision20240709.Parsers.Handshake;
using Turbo.Revision20240709.Serializers.Handshake;

namespace Turbo.Revision20240709;

public class Revision20240709 : IRevision
{
    public string Revision => "WIN63-202407091256-704579380";

    public IDictionary<int, IParser> Parsers { get; } =
        new Dictionary<int, IParser>
        {
            {
                (int)MessageEvent.InitDiffieHandshakeMessageEvent,
                new InitDiffieHandshakeMessageParser()
            },
            {
                (int)MessageEvent.CompleteDiffieHandshakeMessageEvent,
                new CompleteDiffieHandshakeMessageParser()
            },
        };

    public IDictionary<Type, ISerializer> Serializers { get; } =
        new Dictionary<Type, ISerializer>
        {
            { typeof(InitDiffieHandshakeComposer), new InitDiffieHandshakeSerializer() },
            { typeof(CompleteDiffieHandshakeComposer), new CompleteDiffieHandshakeSerializer() },
        };
}
