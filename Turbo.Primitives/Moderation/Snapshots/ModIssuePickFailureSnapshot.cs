using Orleans;

namespace Turbo.Primitives.Moderation.Snapshots;

/// <summary>An issue a moderator could not pick, and who holds it (<c>IssuePickFailedMessageParser</c>).</summary>
[GenerateSerializer, Immutable]
public sealed record ModIssuePickFailureSnapshot
{
    [Id(0)]
    public required int IssueId { get; init; }

    [Id(1)]
    public required int PickerUserId { get; init; }

    [Id(2)]
    public required string PickerUserName { get; init; }
}
