using System;
using System.Collections.Generic;
using Turbo.DefaultRevision.Parsers.Handshake;
using Turbo.Networking.Abstractions.Revisions;
using Turbo.Packets.Abstractions;

namespace Turbo.DefaultRevision;

public class RevisionDefault : IRevision
{
    public string Revision => "Default";

    public IDictionary<int, IParser> Parsers { get; } =
        new Dictionary<int, IParser>
        {
            { (int)MessageEvent.ClientHelloMessageEvent, new ClientHelloParser() },
        };

    public IDictionary<Type, ISerializer> Serializers { get; } =
        new Dictionary<Type, ISerializer> { };
}
