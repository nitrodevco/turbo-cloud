namespace Turbo.Primitives.Settings.Enums;

/// <summary>Where a setting's value comes from, lowest first: each overrides those before it.</summary>
public enum ServerSettingSource
{
    /// <summary>Nothing sets it: the default its config class ships with.</summary>
    Default = 0,

    /// <summary><c>appsettings.json</c>, or the environment's <c>appsettings.{Environment}.json</c>.</summary>
    AppSettings = 1,

    /// <summary>Set in the admin panel (<c>server_settings</c>).</summary>
    Panel = 2,

    /// <summary>An environment variable or the command line, which the panel can't override.</summary>
    Environment = 3,
}
