using System;

namespace Turbo.Rooms.Exceptions;

/// <summary>Raised when a wired parameter is read or written as a type the rule does not declare.</summary>
public sealed class WiredParamTypeMismatchException(
    int parameterIndex,
    Type? declaredType,
    Type requestedType
)
    : Exception(
        $"Wired parameter {parameterIndex} is '{declaredType?.Name ?? "unset"}', not '{requestedType.Name}'."
    )
{
    public int ParameterIndex { get; } = parameterIndex;

    public Type? DeclaredType { get; } = declaredType;

    public Type RequestedType { get; } = requestedType;
}
