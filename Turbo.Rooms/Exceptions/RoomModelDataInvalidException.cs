using System;

namespace Turbo.Rooms.Exceptions;

/// <summary>Raised when stored room model data cannot be compiled into a usable map.</summary>
public sealed class RoomModelDataInvalidException(string reason)
    : Exception($"Room model data is not valid: {reason}")
{
    public string Reason { get; } = reason;
}
