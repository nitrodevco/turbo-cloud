namespace Turbo.Main.Settings;

/// <summary>The admin panel's view of the server's settings (<c>Turbo:Settings</c>).</summary>
public sealed class ServerSettingsConfig
{
    public const string SECTION_NAME = "Turbo:Settings";

    /// <summary>Changes per page of the settings' history.</summary>
    public int HistoryPageSize { get; init; } = 50;

    /// <summary>
    /// How often a silo asks the database whether the panel's overrides changed: how soon one
    /// saved on another silo reaches this one's configuration. 0 never asks; it waits on a restart.
    /// </summary>
    public int CheckSeconds { get; init; } = 5;
}
