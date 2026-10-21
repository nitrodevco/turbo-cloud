using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Rooms.Wired.Link;

/// <summary>
/// What a create, rename, delete or save did: the result code, the Link's connection when it
/// changed, the network a delete removed, the owner's networks as they are now, and the other
/// Links a delete disconnected.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record WiredLinkOperationOutcome
{
    [Id(0)]
    public required int ResultCode { get; init; }

    /// <summary>The Link's network after the request; null when the request did not change it.</summary>
    [Id(1)]
    public string? SelectedNetworkId { get; init; }

    [Id(2)]
    public bool ReceiveLocal { get; init; }

    [Id(3)]
    public string RemovedNetworkId { get; init; } = "0";

    [Id(4)]
    public ImmutableArray<WiredLinkNetworkSnapshot> Networks { get; init; } = [];

    /// <summary>Links a deleted network was connected to, besides the one the request came from.</summary>
    [Id(5)]
    public ImmutableArray<WiredLinkEndpoint> Disconnected { get; init; } = [];

    public static WiredLinkOperationOutcome Failed(int resultCode) =>
        new() { ResultCode = resultCode };
}
