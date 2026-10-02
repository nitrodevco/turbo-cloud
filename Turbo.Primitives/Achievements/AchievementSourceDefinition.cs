using Turbo.Primitives.Achievements.Enums;

namespace Turbo.Primitives.Achievements;

public sealed record AchievementSourceDefinition(
    string Key,
    int Version,
    AchievementReducer Reducer
);
