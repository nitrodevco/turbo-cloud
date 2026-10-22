using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Players.Onboarding.Snapshots;

/// <summary>
/// One step of the new user gift offer (<c>NewUserExperienceGiftOptions</c>): the day and step it
/// belongs to, which the client sends back with the player's pick, and the gifts to pick from.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record NuxGiftStepSnapshot
{
    [Id(0)]
    public required int DayIndex { get; init; }

    [Id(1)]
    public required int StepIndex { get; init; }

    [Id(2)]
    public required ImmutableArray<NuxGiftOptionSnapshot> Options { get; init; }
}
