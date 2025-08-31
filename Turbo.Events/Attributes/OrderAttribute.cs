using System;

namespace Turbo.Events.Attributes;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class OrderAttribute(int value) : Attribute
{
    public int Value { get; } = value;
}
