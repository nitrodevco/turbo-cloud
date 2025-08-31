using System;

namespace Turbo.Events.Attributes;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public class EventTagAttribute(string tag) : Attribute
{
    public string Tag { get; } = tag;
}
