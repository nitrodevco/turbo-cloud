using System;

namespace Turbo.Database.Attributes;

[AttributeUsage(AttributeTargets.Property, Inherited = false)]
public sealed class EnumStorageAttribute(Type? underlying = null) : Attribute
{
    public Type? Underlying { get; } = underlying;
}
