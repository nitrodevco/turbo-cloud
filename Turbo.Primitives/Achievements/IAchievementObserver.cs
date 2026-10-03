using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Primitives.Achievements;

/// <summary>
/// Reacts to a completed achievement level. Notification is best effort: it runs after the award
/// commits, off the player's achievement grain, and a crash between the commit and the call loses
/// it. Anything that must happen exactly once belongs in an <see cref="IAchievementRewardHandler"/>,
/// which is delivered durably and retried. Events for one player are not guaranteed to arrive in
/// order; use <see cref="AchievementLevelCompleted.EarnedAtUtc"/> and
/// <see cref="AchievementLevelCompleted.Level"/>. A throwing observer is logged and never blocks
/// delivery or other observers.
/// </summary>
public interface IAchievementObserver
{
    Task OnLevelCompletedAsync(AchievementLevelCompleted completed, CancellationToken ct);
}
