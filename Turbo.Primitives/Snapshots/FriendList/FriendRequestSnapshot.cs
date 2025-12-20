using Orleans;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Snapshots.FriendList;

[GenerateSerializer, Immutable]
public record FriendRequestSnapshot
{
    [Id(0)]
    public required int RequestId { get; init; }

    [Id(1)]
    public required string RequesterName { get; init; }

    [Id(2)]
    public required string FigureString { get; init; }

    [Id(3)]
    public required PlayerId RequesterUserId { get; init; }
}
