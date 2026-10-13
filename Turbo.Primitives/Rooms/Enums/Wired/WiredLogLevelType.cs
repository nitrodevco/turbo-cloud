namespace Turbo.Primitives.Rooms.Enums.Wired;

/// <summary>
/// The "write to logs" levels by the codes its editor saves (AS3 WIN63-202610131318
/// roomevents/userdefinedroomevents/actiontypes/_-P2U.as:67-70) and the log list shows
/// (<c>wiredmenu.logs_overview.log_level.&lt;n&gt;</c>: 0 INFO, 1 WARN, 2 ERROR, 3 DEBUG).
/// </summary>
public enum WiredLogLevelType
{
    Info = 0,
    Warning = 1,
    Error = 2,
    Debug = 3,
}
