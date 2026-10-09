using Orleans;

namespace Turbo.Primitives.Guilds.Forums.Snapshots;

/// <summary>A forum as a list row shows it (AS3 ForumData).</summary>
[GenerateSerializer, Immutable]
public record GuildForumSnapshot
{
    [Id(0)]
    public required int GroupId { get; init; }

    [Id(1)]
    public required string Name { get; init; }

    [Id(2)]
    public required string Description { get; init; }

    /// <summary>The group's badge code.</summary>
    [Id(3)]
    public required string Icon { get; init; }

    [Id(4)]
    public required int TotalThreads { get; init; }

    /// <summary>The "Score": posts in the last 7 days, the measure the Most Active list sorts by.</summary>
    [Id(5)]
    public required int LeaderboardScore { get; init; }

    [Id(6)]
    public required int TotalMessages { get; init; }

    [Id(7)]
    public required int UnreadMessages { get; init; }

    /// <summary>The forum's last message, numbered within the forum.</summary>
    [Id(8)]
    public required int LastMessageId { get; init; }

    [Id(9)]
    public required int LastMessageAuthorId { get; init; }

    [Id(10)]
    public required string LastMessageAuthorName { get; init; }

    [Id(11)]
    public required int LastMessageSecondsAgo { get; init; }
}
