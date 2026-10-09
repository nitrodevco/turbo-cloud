namespace Turbo.Main.Settings;

/// <summary>The admin panel's view of the server's settings (<c>Turbo:Settings</c>).</summary>
public sealed class ServerSettingsConfig
{
    public const string SECTION_NAME = "Turbo:Settings";

    /// <summary>Changes per page of the settings' history.</summary>
    public int HistoryPageSize { get; init; } = 50;
}
