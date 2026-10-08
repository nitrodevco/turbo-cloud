using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Enums.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Counters;

/// <summary>
/// Where a counter is, as the official client's Creator Tools show it (<c>~clock.state</c>):
/// never started or reset, counting, or held.
/// </summary>
public enum WiredClockState
{
    Initial = 0,
    Running = 1,
    Paused = 2,
}

/// <summary>
/// A counter the wired clock boxes and the <c>~clock.*</c> variables work with: the wired
/// counters, which count up, and the game timers, which count a game down.
/// </summary>
public interface IWiredClock
{
    /// <summary>Its time in pulses, two to the second: elapsed for a counter, left for a timer.</summary>
    public int HalfSeconds { get; }

    public WiredClockState ClockState { get; }

    /// <summary>
    /// Whether it goes with the room's game (<c>~clock.is_game_aware</c>): the game counters and
    /// game timers do, the plain wired counters do not.
    /// </summary>
    public bool IsGameAware { get; }

    public Task ControlAsync(WiredClockControlType control, CancellationToken ct);

    public Task AdjustAsync(WiredOperatorType op, int halfSeconds, CancellationToken ct);

    /// <summary>The room's game ended: a game-aware clock that is counting holds.</summary>
    public Task OnGameEndedAsync(CancellationToken ct);
}
