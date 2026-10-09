using System.Diagnostics.CodeAnalysis;
using Orleans;
using Turbo.Primitives.Guilds.Forums.Enums;

namespace Turbo.Primitives.Guilds.Forums.Snapshots;

/// <summary>A forum with its settings and what the viewer may do in it (AS3 ExtendedForumData).</summary>
[GenerateSerializer, Immutable]
public sealed record GuildForumDetailSnapshot : GuildForumSnapshot
{
    public GuildForumDetailSnapshot() { }

    [SetsRequiredMembers]
    public GuildForumDetailSnapshot(GuildForumSnapshot forum)
        : base(forum) { }

    [Id(0)]
    public required GuildForumPermission ReadPermission { get; init; }

    [Id(1)]
    public required GuildForumPermission PostMessagePermission { get; init; }

    [Id(2)]
    public required GuildForumPermission PostThreadPermission { get; init; }

    [Id(3)]
    public required GuildForumPermission ModeratePermission { get; init; }

    [Id(4)]
    public required string ReadError { get; init; }

    [Id(5)]
    public required string PostMessageError { get; init; }

    [Id(6)]
    public required string PostThreadError { get; init; }

    [Id(7)]
    public required string ModerateError { get; init; }

    [Id(8)]
    public required string ReportError { get; init; }

    [Id(9)]
    public required bool CanChangeSettings { get; init; }

    [Id(10)]
    public required bool IsStaff { get; init; }
}
