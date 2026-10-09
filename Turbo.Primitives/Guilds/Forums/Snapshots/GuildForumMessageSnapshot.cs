using Orleans;
using Turbo.Primitives.Guilds.Forums.Enums;

namespace Turbo.Primitives.Guilds.Forums.Snapshots;

/// <summary>A message as the client's MessageData reads it.</summary>
[GenerateSerializer, Immutable]
public sealed record GuildForumMessageSnapshot
{
    /// <summary>Numbered within the forum, which is what the read marker counts.</summary>
    [Id(0)]
    public required int MessageId { get; init; }

    /// <summary>Its place in the thread, from 0.</summary>
    [Id(1)]
    public required int MessageIndex { get; init; }

    [Id(2)]
    public required int AuthorId { get; init; }

    [Id(3)]
    public required string AuthorName { get; init; }

    [Id(4)]
    public required string AuthorFigure { get; init; }

    [Id(5)]
    public required int CreatedSecondsAgo { get; init; }

    [Id(6)]
    public required string Text { get; init; }

    [Id(7)]
    public required GuildForumState State { get; init; }

    [Id(8)]
    public required int ModeratorId { get; init; }

    [Id(9)]
    public required string ModeratorName { get; init; }

    [Id(10)]
    public required int ModeratedSecondsAgo { get; init; }

    /// <summary>The author's messages in this forum.</summary>
    [Id(11)]
    public required int AuthorPostCount { get; init; }
}
