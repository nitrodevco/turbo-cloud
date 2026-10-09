using Orleans;

namespace Turbo.Primitives.Moderation.Snapshots;

/// <summary>
/// One sanction in force on a player, as the help window's sanction info lists it. The client
/// shows <see cref="Description"/> as it is and skips an entry without one; a gradual sanction
/// adds the probation left and the sanction that would come next.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record SanctionStatusSnapshot
{
    [Id(0)]
    public required SanctionTypeSnapshot Type { get; init; }

    [Id(1)]
    public required string Description { get; init; }

    [Id(2)]
    public required bool Gradual { get; init; }

    [Id(3)]
    public required int ProbationHoursLeft { get; init; }

    [Id(4)]
    public required SanctionTypeSnapshot NextType { get; init; }
}
