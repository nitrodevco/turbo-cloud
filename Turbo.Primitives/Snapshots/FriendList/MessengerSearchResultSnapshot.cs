using Orleans;
using Turbo.Contracts.Enums.Rooms.Object;

namespace Turbo.Primitives.Snapshots.FriendList;

[GenerateSerializer, Immutable]
public record MessengerSearchResultSnapshot
{
    [Id(0)]
    public required long Id { get; init; }

    [Id(1)]
    public required string Name { get; init; }

    [Id(2)]
    public required string Motto { get; init; }

    [Id(3)]
    public required bool Online { get; init; }

    [Id(4)]
    public required bool FollowingAllowed { get; init; }

    [Id(5)]
    public required string UnknownString { get; init; }

    [Id(6)]
    public required AvatarGenderEnum Gender { get; init; }

    [Id(7)]
    public required string Figure { get; init; }

    [Id(8)]
    public required string RealName { get; init; }
}
