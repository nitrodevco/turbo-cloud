using Orleans;
using Turbo.Primitives.Players.Enums.Messenger;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Players.Messenger;

[GenerateSerializer]
public sealed record MessengerFriendDto
{
    [Id(0)]
    public required PlayerId PlayerId { get; init; }

    [Id(1)]
    public string Name { get; set; } = string.Empty;

    [Id(2)]
    public AvatarGenderType Gender { get; set; } = AvatarGenderType.Male;

    [Id(3)]
    public bool Online { get; set; } = false;

    [Id(4)]
    public bool FollowingAllowed { get; set; } = false;

    [Id(5)]
    public string Figure { get; set; } = string.Empty;

    [Id(6)]
    public int CategoryId { get; set; } = -1;

    [Id(7)]
    public string Motto { get; set; } = string.Empty;

    [Id(14)]
    public string LastAccess { get; set; } = string.Empty;

    [Id(8)]
    public string RealName { get; set; } = string.Empty;

    [Id(9)]
    public string FacebookId { get; set; } = string.Empty;

    [Id(10)]
    public bool PersistedMessageUser { get; set; } = false;

    [Id(11)]
    public bool VipMember { get; set; } = false;

    [Id(12)]
    public bool PocketHabboUser { get; set; } = false;

    [Id(13)]
    public MessengerFriendRelationType RelationshipStatus { get; set; } =
        MessengerFriendRelationType.Zero;
}
