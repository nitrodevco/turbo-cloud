using System;

namespace Turbo.Rooms.Exceptions;

/// <summary>Raised when a room model is requested by id and no such model is loaded.</summary>
public sealed class RoomModelNotFoundException(int modelId)
    : Exception($"Room model '{modelId}' could not be found.")
{
    public int ModelId { get; } = modelId;
}
