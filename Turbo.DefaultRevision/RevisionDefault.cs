using System;
using System.Collections.Generic;
using Turbo.Core.Packets.Messages;
using Turbo.Core.Packets.Revisions;
using Turbo.DefaultRevision.Parsers;

namespace Turbo.DefaultRevision;

public class RevisionDefault : IRevision
{
    public string Revision => "Default";

    public IDictionary<int, IParser> Parsers { get; } = new Dictionary<int, IParser>
    {
        { (int)MessageEvent.ClientHelloMessageEvent, new ClientHelloParser() },
    };

    public IDictionary<Type, ISerializer> Serializers { get; } = new Dictionary<Type, ISerializer>
    {
    };
}