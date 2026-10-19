using Orleans;

namespace Turbo.Primitives.Moderation.Snapshots;

/// <summary>
/// A player in the user classification window (<c>UserClassificationData</c>): who they are and
/// their class, one of <see cref="UserClassifications"/>.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record UserClassificationSnapshot
{
    [Id(0)]
    public required int UserId { get; init; }

    [Id(1)]
    public required string UserName { get; init; }

    [Id(2)]
    public required string ClassType { get; init; }
}
