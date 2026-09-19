namespace Turbo.Primitives.Rooms.Enums.Wired;

/// <summary>The five commands of the "control clock" action (client radio clock_control.0..4).</summary>
public enum WiredClockControlType
{
    Start = 0,
    Stop = 1,
    Reset = 2,
    Restart = 3,
    Toggle = 4,
}
