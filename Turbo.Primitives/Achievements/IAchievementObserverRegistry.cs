using System;
using System.Collections.Generic;

namespace Turbo.Primitives.Achievements;

public interface IAchievementObserverRegistry
{
    /// <summary>Registers a batch; disposing the result removes it. A repeated observer rejects the whole batch.</summary>
    IDisposable Register(IEnumerable<IAchievementObserver> observers);

    /// <summary>Hands the event to every registered observer without waiting for them.</summary>
    void NotifyLevelCompleted(AchievementLevelCompleted completed);
}
