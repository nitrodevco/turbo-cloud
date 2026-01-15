using System;

namespace Turbo.Primitives.Rooms.Wired;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class WiredVariableAttribute(string key) : Attribute
{
    public string Key { get; } = key;
}
