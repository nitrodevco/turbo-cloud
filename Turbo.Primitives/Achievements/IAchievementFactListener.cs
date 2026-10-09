using Turbo.Primitives.Players;

namespace Turbo.Primitives.Achievements;

/// <summary>
/// Hears every valid fact as it is recorded, whether or not an achievement counts it, for the
/// progressions that count the same player actions (reward tracks). It runs inside the caller's
/// unit of work, so it must not block: hand the fact on and return.
/// </summary>
public interface IAchievementFactListener
{
    void OnFactRecorded(PlayerId playerId, AchievementFact fact);
}
