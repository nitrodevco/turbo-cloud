using System;

namespace Turbo.Primitives.Commands;

/// <summary>A literal path leading to a concrete arguments record derived from the annotated base record.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class CommandBranchAttribute(string path, Type argumentsType) : Attribute
{
    public string Path { get; } = path;
    public Type ArgumentsType { get; } = argumentsType;
    public string? Permission { get; init; }
}
