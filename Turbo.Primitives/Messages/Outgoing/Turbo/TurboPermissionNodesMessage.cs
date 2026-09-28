using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Turbo;

/// <summary>
/// A Turbo extension, not Habbo's (<c>permission.nodes</c>): every client-facing permission node
/// the player holds, the whole set each time. Sent only to a session that accepted the
/// extension, after <c>UserRights</c> whenever either changes.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record TurboPermissionNodesMessage : IComposer
{
    [Id(0)]
    public required ImmutableArray<string> Nodes { get; init; }
}
