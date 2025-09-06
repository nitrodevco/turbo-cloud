using System;

namespace Turbo.Pipeline.Abstractions.Attributes;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class OrderAttribute(int value) : Attribute
{
    public int Value { get; } = value;
}
