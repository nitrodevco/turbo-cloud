using System;
using System.Collections.Generic;
using Turbo.Primitives.Packets;

namespace Turbo.Primitives.Networking.Revisions;

public interface IRevision
{
    public string Revision { get; }

    public IDictionary<int, IParser> Parsers { get; }

    public IDictionary<Type, ISerializer> Serializers { get; }

    /// <summary>
    /// The serializers that stand in for one of <see cref="Serializers"/> for a session whose
    /// client named a protocol feature in its hello (<see cref="Features.ClientFeatures"/>):
    /// feature name, then composer type. A composer type two of a session's features both
    /// claim is written by the feature whose name sorts first (ordinal).
    /// </summary>
    public IReadOnlyDictionary<
        string,
        IReadOnlyDictionary<Type, ISerializer>
    > FeatureSerializers { get; }
}
