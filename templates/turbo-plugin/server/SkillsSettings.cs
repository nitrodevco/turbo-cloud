namespace Skills;

/// <summary>
/// The plugin's settings: bound to <c>Skills</c> in the hotel's settings, listed on the admin
/// panel's Settings page while the plugin is loaded, and a save there reaches the plugin at once.
/// </summary>
public sealed class SkillsSettings
{
    public const string SECTION_NAME = "Skills";

    /// <summary>Experience one press of Train gives.</summary>
    public int ExperiencePerTrain { get; init; } = 25;

    /// <summary>Experience each level needs.</summary>
    public int ExperiencePerLevel { get; init; } = 100;
}
