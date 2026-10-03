using System.Text.Json.Serialization;

namespace Turbo.Primitives.Achievements.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AchievementReducer
{
    Counter,
    Distinct,
    Maximum,
    CalendarStreak,
    ElapsedSeconds,
    Rank,
}
