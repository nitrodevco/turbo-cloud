using System;
using Turbo.Primitives.Commands.Enums;

namespace Turbo.Primitives.Commands;

/// <summary>A stateless plugin parser. Parsing must be pure; domain changes belong in execution.</summary>
public interface ICommandArgumentParser
{
    Type ValueType { get; }
    CommandParameterKind Kind { get; }
    bool TryParse(string text, out object? value);
}
