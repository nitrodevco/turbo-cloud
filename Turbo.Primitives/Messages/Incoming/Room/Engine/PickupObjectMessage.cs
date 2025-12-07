using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Room.Engine;

public record PickupObjectMessage : IMessageEvent
{
    public required int CategoryId { get; init; }
    public required int ObjectId { get; init; }
    public required bool Confirm { get; init; }
}
