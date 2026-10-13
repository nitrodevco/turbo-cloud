using Orleans;

namespace Turbo.Primitives.Moderation.Snapshots;

/// <summary>
/// A call for help as the moderators' issue queue reads it (<c>IssueInfoMessageParser</c> /
/// <c>IssueMessageData</c>): state 1 open, 2 picked, 3 closed; the source it came from
/// (<c>IssueCategoryNames.getSourceName</c>) and its topic. Issues with the same grouping id and
/// reported player are one bundle (<c>IssueBundle.matches</c>); the lowest priority comes first.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record ModIssueSnapshot
{
    public const int STATE_OPEN = 1;
    public const int STATE_PICKED = 2;
    public const int STATE_CLOSED = 3;

    [Id(0)]
    public required int IssueId { get; init; }

    [Id(1)]
    public required int State { get; init; }

    [Id(2)]
    public required int CategoryId { get; init; }

    [Id(3)]
    public required int ReportedCategoryId { get; init; }

    [Id(4)]
    public required int IssueAgeInMilliseconds { get; init; }

    [Id(5)]
    public required int Priority { get; init; }

    [Id(6)]
    public required int GroupingId { get; init; }

    [Id(7)]
    public required int ReporterUserId { get; init; }

    [Id(8)]
    public required string ReporterUserName { get; init; }

    [Id(9)]
    public required int ReportedUserId { get; init; }

    [Id(10)]
    public required string ReportedUserName { get; init; }

    [Id(11)]
    public required int PickerUserId { get; init; }

    [Id(12)]
    public required string PickerUserName { get; init; }

    [Id(13)]
    public required string Message { get; init; }

    [Id(14)]
    public required int ChatRecordId { get; init; }
}
