using Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Snapshots.FriendList;

[GenerateSerializer, Immutable]
public record MessengerFriendSnapshot
{
    [Id(0)]
    public required PlayerId PlayerId { get; init; }

    [Id(1)]
    public required string Name { get; init; }

    [Id(2)]
    public required AvatarGenderType Gender { get; init; }

    [Id(3)]
    public required bool Online { get; init; }

    [Id(4)]
    public required bool FollowingAllowed { get; init; }

    [Id(5)]
    public required string Figure { get; init; }

    [Id(6)]
    public required int CategoryId { get; init; }

    [Id(7)]
    public required string Motto { get; init; }

    [Id(8)]
    public required string RealName { get; init; }

    [Id(9)]
    public required string FacebookId { get; init; }

    [Id(10)]
    public required bool PersistedMessageUser { get; init; }

    [Id(11)]
    public required bool VipMember { get; init; }

    [Id(12)]
    public required bool PocketHabboUser { get; init; }

    [Id(13)]
    public required short RelationshipStatus { get; init; }
}
