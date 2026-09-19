using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Room.Engine;

/// <summary>Place a pet from the inventory; (0, 0) when no tile was chosen.</summary>
public record PlacePetMessage : IMessageEvent
{
    public required int PetId { get; init; }
    public required int X { get; init; }
    public required int Y { get; init; }
}
