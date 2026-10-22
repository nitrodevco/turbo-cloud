using Orleans;

namespace Turbo.Primitives.Players.Onboarding.Snapshots;

/// <summary>
/// A player's pick in one step of the gift offer (<c>NewUserExperienceGetGiftsSelection</c>): the
/// step's day and index as offered, and the gift's place in it.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record NuxGiftSelectionSnapshot
{
    [Id(0)]
    public required int DayIndex { get; init; }

    [Id(1)]
    public required int StepIndex { get; init; }

    [Id(2)]
    public required int GiftIndex { get; init; }
}
