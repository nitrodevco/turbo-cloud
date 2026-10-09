using Orleans;

namespace Turbo.Primitives.Guilds.Forums.Snapshots;

/// <summary>
/// One entry of UpdateForumReadMarker: the forum, the last message read (numbered within the
/// forum), and whether the whole forum is to be marked read.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record GuildForumReadMarkerSnapshot
{
    [Id(0)]
    public required int GroupId { get; init; }

    [Id(1)]
    public required int LastReadMessageId { get; init; }

    [Id(2)]
    public required bool MarkAll { get; init; }
}
