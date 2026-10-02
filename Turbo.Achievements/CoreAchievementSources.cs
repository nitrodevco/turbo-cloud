using System.Collections.Immutable;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Achievements.Enums;

namespace Turbo.Achievements;

/// <summary>
/// The facts the hotel itself records. They belong to the engine rather than to any catalog,
/// because the gameplay that produces them is in Turbo; a pack only decides which of them become
/// achievements. A source may allow more reducers than its usual one so a hotel can count the
/// same facts another way in data alone.
/// </summary>
public static class CoreAchievementSources
{
    public static ImmutableArray<AchievementSourceDefinition> All { get; } =
    [
        new(AchievementSources.ONLINE, 1, AchievementReducer.ElapsedSeconds),
        new(
            AchievementSources.LOGIN,
            1,
            AchievementReducer.CalendarStreak,
            [AchievementReducer.Counter, AchievementReducer.Distinct]
        ),
        new(AchievementSources.ACCOUNT_AGE, 1, AchievementReducer.Maximum),
        new(AchievementSources.FIGURE, 1, AchievementReducer.Counter),
        new(AchievementSources.MOTTO, 1, AchievementReducer.Counter),
        new(AchievementSources.HC, 1, AchievementReducer.Maximum),
        new(AchievementSources.PURCHASED_HC, 1, AchievementReducer.Maximum),
        new(AchievementSources.VISIT, 1, AchievementReducer.Distinct, [AchievementReducer.Counter]),
        new(AchievementSources.FURNITURE, 1, AchievementReducer.Counter),
        new(AchievementSources.RESPECT_GIVEN, 1, AchievementReducer.Counter),
        new(AchievementSources.RESPECT_RECEIVED, 1, AchievementReducer.Counter),
        new(AchievementSources.PETS, 1, AchievementReducer.Maximum),
        new(AchievementSources.NUTRITION, 1, AchievementReducer.Counter),
        new(AchievementSources.PET_LEVEL, 1, AchievementReducer.Counter),
        new(AchievementSources.PET_RESPECT_GIVEN, 1, AchievementReducer.Counter),
        new(AchievementSources.PET_RESPECT_RECEIVED, 1, AchievementReducer.Counter),
        new(AchievementSources.FLOOR_HEIGHTS, 1, AchievementReducer.Maximum),
        new(AchievementSources.ROOM_RANK, 1, AchievementReducer.Rank),
        // Nothing records it, so any reducer may be chosen until the definition is pointed at a
        // source something does.
        new(
            AchievementSources.UNHOOKED,
            1,
            AchievementReducer.Counter,
            [
                AchievementReducer.Distinct,
                AchievementReducer.Maximum,
                AchievementReducer.CalendarStreak,
                AchievementReducer.ElapsedSeconds,
                AchievementReducer.Rank,
            ]
        ),
    ];
}
