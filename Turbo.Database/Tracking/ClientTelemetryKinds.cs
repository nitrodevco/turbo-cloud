namespace Turbo.Database.Tracking;

/// <summary>
/// The kinds of daily figure in <c>client_telemetry_daily</c>, with what their keys and values are.
/// </summary>
public static class ClientTelemetryKinds
{
    /// <summary>Frames per second from performance reports; keys browser, system, device.</summary>
    public const string FPS = "fps";

    /// <summary>Client memory in MB from performance reports; keys browser, system, device.</summary>
    public const string MEMORY = "memory";

    /// <summary>Slow frames (the sum) per performance report; keys browser, system, device.</summary>
    public const string SLOW_UPDATES = "slow_updates";

    /// <summary>Average ping in ms from latency reports; keys browser, system, device.</summary>
    public const string LATENCY = "latency";

    /// <summary>Chat lag warnings (the sum) per report, every room and none; no keys.</summary>
    public const string LAG = "lag";

    /// <summary>Frames per second in a room, from performance reports and fps events; key room id.</summary>
    public const string ROOM_FPS = "room_fps";

    /// <summary>Average ping in ms in a room; key room id.</summary>
    public const string ROOM_LATENCY = "room_latency";

    /// <summary>Chat lag warnings (the sum) in a room; key room id.</summary>
    public const string ROOM_LAG = "room_lag";

    /// <summary>Events logged; keys category, type, action.</summary>
    public const string EVENT = "event";

    /// <summary>Logins that reached a step, with its time from the socket opening in ms; key step.</summary>
    public const string LOGIN = "login";

    /// <summary>The marker that a day is rolled up; its samples are the raw rows it was made from.</summary>
    public const string DAY = "day";

    /// <summary>
    /// The event category Habbo's client sends its frame rate in (<c>HabboTracking.as</c>
    /// <c>trackEventLog("ClientPerformance", fps, "fps", ..., roomId)</c>): counted as a room's
    /// frame rate, not as usage.
    /// </summary>
    public const string CLIENT_PERFORMANCE_CATEGORY = "ClientPerformance";
}
