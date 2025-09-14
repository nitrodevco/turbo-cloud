using System;

namespace Turbo.Contracts.Attributes;

[AttributeUsage(AttributeTargets.Assembly, Inherited = false, AllowMultiple = false)]
public class TurboScan(string scope = "default") : Attribute
{
    public string Scope { get; } = scope;
}
