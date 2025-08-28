using System;
using System.Collections.Generic;
using Turbo.Core.Packets.Messages;
using Turbo.Core.Packets.Revisions;
using Turbo.DefaultRevision.Parsers;
using Turbo.DefaultRevision.Parsers.Handshake;
using Turbo.DefaultRevision.Serializers.Handshake;
using Turbo.Packets.Outgoing.Handshake;

namespace Turbo.DefaultRevision;

public class RevisionDefault : IRevision
{
    public string Revision => "Default";

    public IDictionary<int, IParser> Parsers { get; } =
        new Dictionary<int, IParser>
        {
            { (int)MessageEvent.ClientHelloMessageEvent, new ClientHelloParser() },
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
