using System;
using System.Collections.Generic;
using Turbo.Primitives.Packets;

namespace Turbo.Primitives.Networking.Revisions;

public interface IRevision
{
    public string Revision { get; }

    public IDictionary<int, IParser> Parsers { get; }

    public IDictionary<Type, ISerializer> Serializers { get; }
}
