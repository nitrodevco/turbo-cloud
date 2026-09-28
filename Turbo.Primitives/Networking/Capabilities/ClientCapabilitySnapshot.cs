using Orleans;

namespace Turbo.Primitives.Networking.Capabilities;

/// <summary>One protocol extension, by name, at a version: asked for by a client or accepted by the server.</summary>
[GenerateSerializer, Immutable]
public sealed record ClientCapabilitySnapshot
{
    [Id(0)]
    public required string Name { get; init; }

    [Id(1)]
    public required int Version { get; init; }
}
