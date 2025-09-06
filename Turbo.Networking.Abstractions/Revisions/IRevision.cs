using System;
using System.Collections.Generic;
using Turbo.Packets.Abstractions;

namespace Turbo.Networking.Abstractions.Revisions;

public interface IRevision
{
    public string Revision { get; }

    public IDictionary<int, IParser> Parsers { get; }

    public IDictionary<Type, ISerializer> Serializers { get; }
}
