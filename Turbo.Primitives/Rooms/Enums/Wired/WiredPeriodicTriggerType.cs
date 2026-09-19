namespace Turbo.Primitives.Rooms.Enums.Wired;

public enum WiredPeriodicTriggerType
{
    Short = 0,
    Long = 1,

    /// <summary>The plain "periodically" box: the value is in half-second pulses.</summary>
    Normal = 2,
}
