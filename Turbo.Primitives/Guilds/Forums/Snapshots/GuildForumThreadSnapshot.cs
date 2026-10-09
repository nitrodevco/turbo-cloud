using Orleans;
using Turbo.Primitives.Guilds.Forums.Enums;

namespace Turbo.Primitives.Guilds.Forums.Snapshots;

/// <summary>A thread as the client's ThreadData reads it.</summary>
[GenerateSerializer, Immutable]
public sealed record GuildForumThreadSnapshot
{
    [Id(0)]
    public required int ThreadId { get; init; }

    [Id(1)]
    public required int AuthorId { get; init; }

    [Id(2)]
    public required string AuthorName { get; init; }

    [Id(3)]
    public required string Subject { get; init; }

    [Id(4)]
    public required bool IsSticky { get; init; }

    [Id(5)]
    public required bool IsLocked { get; init; }

    [Id(6)]
    public required int CreatedSecondsAgo { get; init; }

    [Id(7)]
    public required int TotalMessages { get; init; }

    [Id(8)]
    public required int UnreadMessages { get; init; }

    [Id(9)]
    public required int LastMessageId { get; init; }

    [Id(10)]
    public required int LastMessageAuthorId { get; init; }

    [Id(11)]
    public required string LastMessageAuthorName { get; init; }

    [Id(12)]
    public required int LastMessageSecondsAgo { get; init; }

    [Id(13)]
    public required GuildForumState State { get; init; }

    [Id(14)]
    public required int ModeratorId { get; init; }

    [Id(15)]
    public required string ModeratorName { get; init; }

    [Id(16)]
    public required int ModeratedSecondsAgo { get; init; }
}
