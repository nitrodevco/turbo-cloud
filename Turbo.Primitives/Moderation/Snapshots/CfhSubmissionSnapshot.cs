using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Moderation.Enums;

namespace Turbo.Primitives.Moderation.Snapshots;

/// <summary>
/// A call for help as the client sends it (<c>CallForHelpMessageComposer</c>, or the messenger's
/// and a photo's variants): what the reporter wrote, the topic, who and where, the chat lines
/// they picked, the photo for a photo report, and the name and email the unlawful activity
/// report asks for (empty otherwise). Every value is untrusted.
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

    [Id(7)]
    public CfhSourceType Source { get; init; } = CfhSourceType.Room;

    /// <summary>A photo report's photo: its extra data id; empty otherwise.</summary>
    [Id(8)]
    public string ExtraDataId { get; init; } = string.Empty;

    /// <summary>A photo report's wall item; 0 otherwise.</summary>
    [Id(9)]
    public int ItemId { get; init; }

    /// <summary>A forum report's group; 0 otherwise.</summary>
    [Id(10)]
    public int GroupId { get; init; }

    /// <summary>A forum report's thread; 0 otherwise.</summary>
    [Id(11)]
    public int ThreadId { get; init; }

    /// <summary>A forum message report's message, numbered within the forum; 0 otherwise.</summary>
    [Id(12)]
    public int MessageId { get; init; }
}
