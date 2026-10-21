using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Rooms.Wired.Link;

/// <summary>What a Link's window opens with: its connection, its receive setting and the owner's networks.</summary>
[GenerateSerializer, Immutable]
public sealed record WiredLinkConfigurationSnapshot
{
    /// <summary>The network the Link is connected to; "0" for none.</summary>
    [Id(0)]
    public required string SelectedNetworkId { get; init; }

    [Id(1)]
    public required bool ReceiveLocal { get; init; }

    [Id(2)]
    public required ImmutableArray<WiredLinkNetworkSnapshot> Networks { get; init; }
}
