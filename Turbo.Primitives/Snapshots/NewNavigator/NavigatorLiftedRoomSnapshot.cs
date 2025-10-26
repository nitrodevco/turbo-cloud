using Orleans;

namespace Turbo.Primitives.Snapshots.NewNavigator;

[GenerateSerializer, Immutable]
public record NavigatorLiftedRoomSnapshot
{
    [Id(0)]
    public required int FlatId { get; init; }

    [Id(1)]
    public required int AreaId { get; init; }

    [Id(2)]
    public required string Image { get; init; }

    [Id(3)]
    public required string Caption { get; init; }
}
