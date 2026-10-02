using System;

namespace Turbo.Primitives.Commands;

/// <summary>
/// Declares a class as a chat command: <c>:name args</c> in room chat. The name is lowercase and
/// one word. Aliases are extra names declared in code; a hotel may add more through its texts
/// (<c>command.&lt;name&gt;.aliases</c>) but never rename or remove the declared ones.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class CommandAttribute(string name) : Attribute
{
    public string Name { get; } = name;

    public string Description { get; init; } = string.Empty;

    public string[] Aliases { get; init; } = [];

    /// <summary>
    /// The section <c>:commands</c> lists it under. A plugin names its own (<c>Roleplay</c>); a
    /// command that names none is listed under <see cref="CommandCategories.GENERAL"/>.
    /// </summary>
    public string Category { get; init; } = CommandCategories.GENERAL;
}
