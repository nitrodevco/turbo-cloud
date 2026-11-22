using Turbo.Contracts.Plugins;

namespace Skills;

/// <summary>
/// The plugin's settings: bound to <c>Skills</c> in the hotel's settings, listed on the admin
/// panel's Settings page while the plugin is loaded, and a save there reaches the plugin at once.
/// </summary>
[PluginSettings("Skills")]
public sealed class SkillsSettings
{
    /// <summary>Experience one press of Train gives.</summary>
    public int ExperiencePerTrain { get; init; } = 25;

    /// <summary>Experience each level needs.</summary>
    public int ExperiencePerLevel { get; init; } = 100;
}
