using System;

namespace Turbo.Primitives.Plugins.Messages;

/// <summary>
/// Names an <see cref="IPluginMessage"/>. The name starts with the plugin's key and a dot
/// (<c>skills.state</c> for the plugin <c>skills</c>); a load that declares another is refused, as is
/// a name another plugin already registered.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class PluginMessageAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}
