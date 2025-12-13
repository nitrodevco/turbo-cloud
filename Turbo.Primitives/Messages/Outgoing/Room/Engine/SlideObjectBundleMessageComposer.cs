using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Messages.Outgoing.Room.Engine;

[GenerateSerializer, Immutable]
public sealed record SlideObjectBundleMessageComposer : IComposer
{
    [Id(0)]
    public required int FromX { get; init; }

    [Id(1)]
    public required int FromY { get; init; }

    [Id(2)]
    public required int ToX { get; init; }

    [Id(3)]
    public required int ToY { get; init; }

    [Id(4)]
    public required int RollerItemId { get; init; }

    [Id(5)]
    public required ImmutableArray<(int, double, double)> FloorItemHeights { get; init; }

    [Id(6)]
    public (SlideAvatarMoveType, int, double, double)? Avatar { get; init; }
}
