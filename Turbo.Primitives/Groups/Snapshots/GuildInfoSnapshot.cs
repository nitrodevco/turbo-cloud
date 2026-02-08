using Orleans;

namespace Turbo.Primitives.Groups.Snapshots;

[GenerateSerializer, Immutable]
public sealed record GuildInfoSnapshot
{
    [Id(0)]
    public required int GroupId { get; init; }

    [Id(1)]
    public required string GroupName { get; init; }

    [Id(2)]
    public required string BadgeCode { get; init; }

    [Id(3)]
    public required string PrimaryColor { get; init; }

    [Id(4)]
    public required string SecondaryColor { get; init; }

    [Id(5)]
    public required bool Favourite { get; init; }

    [Id(6)]
    public required int OwnerId { get; init; }

    [Id(7)]
    public required bool HasForum { get; init; }
}
