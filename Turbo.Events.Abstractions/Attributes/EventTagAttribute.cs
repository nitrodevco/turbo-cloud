using System;

namespace Turbo.Events.Abstractions.Attributes;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public class EventTagAttribute(string tag) : Attribute
{
    public string Tag { get; } = tag;
}
