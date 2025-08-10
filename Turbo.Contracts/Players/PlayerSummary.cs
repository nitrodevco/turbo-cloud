using Orleans;

namespace Turbo.Contracts.Players;

[GenerateSerializer]
public record PlayerSummary(
    [property: Id(0)] string Id,
    [property: Id(1)] string Name,
    [property: Id(2)] string Motto,
    [property: Id(3)] string Figure);