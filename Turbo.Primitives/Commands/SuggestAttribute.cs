using System;

namespace Turbo.Primitives.Commands;

/// <summary>
/// Names where the values a client may offer for a word parameter come from: a registered
/// <see cref="ISuggestionSource"/> of that <see cref="Source"/> (<see cref="SuggestionSources"/>
/// for core's own). Only for a <c>string</c> parameter.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
public sealed class SuggestAttribute(string source) : Attribute
{
    public string Source { get; } = source;
}
