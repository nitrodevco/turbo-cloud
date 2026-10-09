using Orleans;
using Turbo.Primitives.Moderation.Enums;

namespace Turbo.Primitives.Moderation.Snapshots;

/// <summary>
/// One call for help the player made, as the help window's "my reports" lists it. Times are
/// milliseconds since the epoch, -1 for not yet.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record CfhReportStatusSnapshot
{
    [Id(0)]
    public required long Id { get; init; }

    [Id(1)]
    public required long CreatedAtMs { get; init; }

    [Id(2)]
    public required string Message { get; init; }

    [Id(3)]
    public required int TopicId { get; init; }

    [Id(4)]
    public required string ReportedName { get; init; }

    [Id(5)]
    public required long ClosedAtMs { get; init; }

    [Id(6)]
    public required bool Sanctioned { get; init; }

    [Id(7)]
    public required bool SanctionedByAutoModeration { get; init; }

    [Id(8)]
    public required CfhAppealStatusType AppealStatus { get; init; }

    [Id(9)]
    public required long AppealCreatedAtMs { get; init; }

    [Id(10)]
    public required long AppealResolvedAtMs { get; init; }
}
