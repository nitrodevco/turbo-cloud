using System;

namespace Turbo.Pipeline.Abstractions.Attributes;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class TagAttribute(string tag) : Attribute
{
    public string Tag { get; } = tag;
}
