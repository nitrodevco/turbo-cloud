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
}
