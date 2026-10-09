using Orleans;

namespace Turbo.Primitives.Moderation.Snapshots;

/// <summary>
/// One thing a call for help can be about. The client shows <c>help.cfh.topic.&lt;id&gt;</c>, finds
/// a topic by <see cref="Name"/> (the room report button asks for
/// <c>inappropiate_room_group_event</c>) and sends the id with the report.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record CfhTopicSnapshot
{
    [Id(0)]
    public required int Id { get; init; }

    [Id(1)]
    public required string Name { get; init; }

    /// <summary>Who deals with it: <c>mods</c>, <c>mods_till_logout</c> or <c>auto_reply</c>.</summary>
    [Id(2)]
    public required string Consequence { get; init; }
}
