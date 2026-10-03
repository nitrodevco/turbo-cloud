using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Achievements;

/// <summary>
/// Records a fact for a plugin without a database context or a transaction of its own to manage.
/// Use it when the thing that happened is already finished (a minigame result, a heist, a quest
/// step). Code that changes the database itself should record in the same unit of work through
/// <c>IAchievementFactRecorder</c> instead, so the fact commits or rolls back with the change.
/// </summary>
public interface IAchievementFacts
{
    /// <summary>
    /// Stores the fact and asks for the player's progress to be updated soon. It returns once the
    /// fact is durable, not once the achievement is awarded, and it never waits for the award, so it
    /// is safe to call from a grain. Recording the same operation id again does nothing, and so
    /// does a fact no enabled achievement listens to. An invalid fact throws
    /// <see cref="System.ArgumentException"/> and stores nothing.
    /// </summary>
    Task RecordAsync(PlayerId playerId, AchievementFact fact, CancellationToken ct);
}
