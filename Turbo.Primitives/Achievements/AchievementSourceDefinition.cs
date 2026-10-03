using System.Collections.Immutable;
using Turbo.Primitives.Achievements.Enums;

namespace Turbo.Primitives.Achievements;

/// <summary>
/// A kind of fact something in the hotel records. <paramref name="Reducer"/> is how it is normally
/// counted; <paramref name="AlsoReducers"/> are other ways a definition may count the same facts
/// (total logins as well as login streaks), so owners can build on a source in data alone.
/// </summary>
public sealed record AchievementSourceDefinition(
    string Key,
    int Version,
    AchievementReducer Reducer,
    ImmutableArray<AchievementReducer> AlsoReducers = default
)
{
    public bool Allows(AchievementReducer reducer) =>
        reducer == Reducer || (!AlsoReducers.IsDefaultOrEmpty && AlsoReducers.Contains(reducer));
}
