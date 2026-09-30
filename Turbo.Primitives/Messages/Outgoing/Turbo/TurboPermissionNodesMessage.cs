using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Networking.Capabilities;

namespace Turbo.Primitives.Messages.Outgoing.Turbo;

/// <summary>
/// A Turbo extension, not Habbo's (<c>permission.nodes</c>): every client-facing permission node
/// the player holds, the whole set each time, whenever it changes. The presence delivers it only
/// to a session that accepted the extension.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record TurboPermissionNodesMessage : ICapabilityComposer
{
    [Id(0)]
    public required ImmutableArray<string> Nodes { get; init; }

    public string Capability => ClientCapabilities.PERMISSION_NODES;
}
