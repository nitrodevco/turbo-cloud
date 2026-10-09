using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Moderation.Snapshots;

/// <summary>
/// A call for help as the client sends it (<c>CallForHelpMessageComposer</c>): what the reporter
/// wrote, the topic, who and where, the chat lines they picked, and the name and email the
/// unlawful activity report asks for (empty otherwise). Every value is untrusted.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record CfhSubmissionSnapshot
{
    [Id(0)]
    public required string Message { get; init; }

    [Id(1)]
    public required int TopicId { get; init; }

    [Id(2)]
    public required int ReportedPlayerId { get; init; }

    [Id(3)]
    public required int RoomId { get; init; }

    [Id(4)]
    public required ImmutableArray<CfhChatLineSnapshot> ChatLines { get; init; }

    [Id(5)]
    public required string Name { get; init; }

    [Id(6)]
    public required string Email { get; init; }
}
