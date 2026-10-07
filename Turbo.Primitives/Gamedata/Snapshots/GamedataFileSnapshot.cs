using System;
using Orleans;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>A built gamedata file, named by the hash of its content, which its address carries.</summary>
[GenerateSerializer, Immutable]
public sealed record GamedataFileSnapshot
{
    /// <summary>Which file (<see cref="GamedataFiles"/>).</summary>
    [Id(0)]
    public required string File { get; init; }

    /// <summary>SHA-1 of the content, in lowercase hex.</summary>
    [Id(1)]
    public required string Hash { get; init; }

    /// <summary>The content's size uncompressed, in bytes.</summary>
    [Id(2)]
    public required int Size { get; init; }

    [Id(3)]
    public required DateTime BuiltAt { get; init; }
}
