using System;
using System.Collections.Generic;

namespace Turbo.Primitives.Achievements;

public interface IAchievementRewardRegistry
{
    IDisposable Register(IEnumerable<IAchievementRewardHandler> handlers);
    bool TryGet(string key, int version, out IAchievementRewardHandler handler);
}
