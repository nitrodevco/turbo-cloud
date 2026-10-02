using System;

namespace Turbo.Primitives.Commands;

/// <summary>
/// Lets a <see cref="PlayerTarget"/> parameter name a group of players, <c>@room</c> or
/// <c>@online</c>, for an executor who holds <see cref="Node"/>. Declared on the parameter, so
/// <c>:commands</c> and a client can tell who may use a selector without running the command. A
/// command may declare it on one parameter; a target without it takes a single player only.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
public sealed class SelectorsAttribute(string node) : Attribute
{
    public string Node { get; } = node;
}
