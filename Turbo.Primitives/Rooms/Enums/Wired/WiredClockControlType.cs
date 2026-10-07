namespace Turbo.Primitives.Rooms.Enums.Wired;

/// <summary>
/// The five commands of the "control clock" action, as its editor offers them
/// (<c>wiredfurni.params.clock_control.0</c> to <c>.4</c>: Start, Stop, Reset, Pause, Resume).
/// </summary>
public enum WiredClockControlType
{
    Start = 0,
    Stop = 1,
    Reset = 2,
    Pause = 3,
    Resume = 4,
}
