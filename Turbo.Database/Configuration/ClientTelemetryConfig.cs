using Turbo.Primitives.Settings;

namespace Turbo.Database.Configuration;

/// <summary>
/// Keeping what clients report about themselves (<c>PerformanceLog</c>, <c>LatencyPingReport</c>,
/// <c>LagWarningReport</c>, <c>EventLog</c>) and the steps of their logins, for the admin panel's
/// Client health and Usage pages. Raw rows are kept for <see cref="RawRetentionDays"/>; each day is
/// rolled up into daily figures kept for <see cref="RollupRetentionDays"/>. No address is stored.
/// </summary>
public class ClientTelemetryConfig
{
    public const string SECTION_NAME = "Turbo:ClientTelemetry";

    /// <summary>Whether reports are kept. Off, they are read and dropped.</summary>
    [LiveSetting]
    public bool Enabled { get; init; } = true;

    /// <summary>Days raw reports, events and login steps are kept; 0 keeps them for good.</summary>
    [LiveSetting]
    public int RawRetentionDays { get; init; } = 30;

    /// <summary>Days the daily figures are kept; 0 keeps them for good.</summary>
    [LiveSetting]
    public int RollupRetentionDays { get; init; } = 365;

    /// <summary>
    /// The most performance, latency and lag reports one player's client may have kept per minute;
    /// Habbo's client sends one of each a minute at most, so more is a client misbehaving.
    /// </summary>
    [LiveSetting]
    public int MaxReportsPerPlayerPerMinute { get; init; } = 12;

    /// <summary>The most events one player's client may have kept per minute.</summary>
    [LiveSetting]
    public int MaxEventsPerPlayerPerMinute { get; init; } = 120;

    /// <summary>
    /// The most rows waiting to be written. Past it new ones are dropped (and counted in the log),
    /// so a slow database never grows the server's memory.
    /// </summary>
    public int QueueCapacity { get; init; } = 20_000;

    /// <summary>The most rows written in one go.</summary>
    public int BatchSize { get; init; } = 500;

    /// <summary>How long rows wait to be written together, in milliseconds.</summary>
    public int FlushIntervalMs { get; init; } = 5_000;

    /// <summary>How often, in minutes, finished days are rolled up and expired rows deleted.</summary>
    public int MaintenanceIntervalMinutes { get; init; } = 60;

    /// <summary>
    /// How long, in minutes, a login is followed after its socket opens; a step later than this
    /// (a first room entered an hour after logging in) is not part of the login.
    /// </summary>
    public int LoginTrackingMinutes { get; init; } = 10;
}
