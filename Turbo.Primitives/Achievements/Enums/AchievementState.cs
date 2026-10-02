using System.Text.Json.Serialization;

namespace Turbo.Primitives.Achievements.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AchievementState : short
{
    Disabled = 0,
    Enabled = 1,
    Archived = 2,
    OffSeason = 3,
    WiredControlled = 4,
}
