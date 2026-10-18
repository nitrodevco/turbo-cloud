using Orleans;

namespace Turbo.Primitives.Rooms.Wired.Link;

/// <summary>One link network of the player's, as the link window lists it.</summary>
[GenerateSerializer, Immutable]
public sealed record WiredLinkNetworkSnapshot
{
    [Id(0)]
    public required string NetworkId { get; init; }

    [Id(1)]
    public required string Name { get; init; }

    [Id(2)]
    public required int ConnectionCount { get; init; }

    [Id(3)]
    public required bool LinkedInCurrentRoom { get; init; }
}
