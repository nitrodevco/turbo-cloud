using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Rooms.Mapping;

[GenerateSerializer, Immutable]
internal sealed class RollerData
{
    [Id(0)]
    public required int RollerId { get; init; }

    [Id(1)]
    public required (int, int) FromXY { get; init; }

    [Id(2)]
    public required (int, int) ToXY { get; init; }

    [Id(3)]
    public required ImmutableArray<(int, double, double)> FloorItemHeights { get; init; }

    [Id(4)]
    public (SlideAvatarMoveType, int, double, double)? Avatar { get; init; }
}
