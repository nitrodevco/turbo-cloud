using System;
using System.Threading;

namespace Turbo.Achievements;

internal sealed class AchievementRegistration(Action dispose) : IDisposable
{
    private Action? _dispose = dispose;

    public void Dispose() => Interlocked.Exchange(ref _dispose, null)?.Invoke();
}
