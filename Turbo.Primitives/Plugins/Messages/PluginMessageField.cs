using System.Reflection;

namespace Turbo.Primitives.Plugins.Messages;

/// <summary>
/// One field of a plugin message or of a record inside one: its wire name (camelCase), the property
/// its value is read from, and its value's schema.
/// </summary>
internal sealed record PluginMessageField(
    string Name,
    PropertyInfo Property,
    PluginMessageValue Value
);
