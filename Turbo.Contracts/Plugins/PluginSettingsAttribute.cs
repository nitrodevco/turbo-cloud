using System;

namespace Turbo.Contracts.Plugins;

/// <summary>
/// A plugin's settings class, bound to <see cref="Section"/> of the hotel's settings as the plugin
/// loads (<c>IOptions&lt;T&gt;</c>, <c>IOptionsMonitor&lt;T&gt;</c>) and listed on the admin panel's
/// Settings page while it is loaded. Nothing to register: the class is found in the plugin's
/// assembly. A class the plugin binds itself is left as it binds it.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class PluginSettingsAttribute(string section) : Attribute
{
    /// <summary>The settings section, such as <c>Skills</c> or <c>Turbo:Skills</c>.</summary>
    public string Section { get; } = section;
}
