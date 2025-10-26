using Orleans;

namespace Turbo.Primitives.Snapshots.NewNavigator;

[GenerateSerializer, Immutable]
public record NavigatorQuickLinkSnapshot
{
    [Id(0)]
    public required int Id { get; init; }

    [Id(1)]
    public required string SearchCode { get; init; }

    [Id(2)]
    public required string Filter { get; init; }

    [Id(3)]
    public required string Localization { get; init; }
}
