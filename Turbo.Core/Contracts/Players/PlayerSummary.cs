namespace Turbo.Core.Contracts.Players;

using Orleans;

[GenerateSerializer]
public record PlayerSummary(
    [property: Id(0)] long PlayerId,
    [property: Id(1)] string Name,
    [property: Id(2)] string Motto,
    [property: Id(3)] string Figure);
