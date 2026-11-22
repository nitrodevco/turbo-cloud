using System;

namespace Turbo.Primitives.Rooms.Wired;

/// <summary>
/// The wired time unit. The client editor counts delays, timers and windows in pulses of half
/// a second; every box converts through here so the unit is defined once.
/// </summary>
public static class WiredPulses
{
    public const int MS = 500;

    public static long ToMs(int pulses) => pulses * (long)MS;

    public static int FromMs(long ms) => (int)(ms / MS);

    /// <summary>The long periodic trigger's unit (<c>wf_trg_period_long</c>): ten pulses, 5 s.</summary>
    public const int LONG_PERIOD_UNIT_MS = 5000;

    /// <summary>As many long units as fit in the int of milliseconds a period is counted in.</summary>
    public const int MAX_LONG_PERIOD_UNITS = int.MaxValue / LONG_PERIOD_UNIT_MS;

    /// <summary>
    /// The step of an action's "precise" delay (AS3 <c>ActionDelaySection</c>, mode
    /// <c>delay.mode.precise</c>: 0 to 1200 steps of 0.05 s).
    /// </summary>
    public const int PRECISE_STEP_MS = 50;

    /// <summary>
    /// An action's saved delay as milliseconds. The editor writes a standard delay as pulses
    /// (0 or more) and a precise one as minus its milliseconds
    /// (<c>encodedDelay = -(steps * 50)</c>), so the sign says which. Both are held to
    /// <paramref name="maxPulses"/> pulses, and a precise one to whole steps.
    /// </summary>
    public static long ActionDelayToMs(int encodedDelay, int maxPulses)
    {
        var maxMs = ToMs(Math.Max(0, maxPulses));

        if (encodedDelay >= 0)
            return Math.Min(ToMs(encodedDelay), maxMs);

        var ms = Math.Min(-(long)encodedDelay, maxMs);

        return ms - ms % PRECISE_STEP_MS;
    }
}
