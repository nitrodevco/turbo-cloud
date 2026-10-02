using Turbo.Database.Context;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Players;

namespace Turbo.Database.Achievements;

/// <summary>Adds a fact to the originating unit of work. The caller commits it with the successful mutation.</summary>
public interface IAchievementFactRecorder
{
    void Record(TurboDbContext db, PlayerId playerId, AchievementFact fact);
}
