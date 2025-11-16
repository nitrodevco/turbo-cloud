using System;

namespace Turbo.Primitives.Rooms.Furniture;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class FurnitureLogicAttribute(string key) : Attribute
{
    public string Key { get; } = key;
}
