using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Room.Engine;

public record ClickFurniMessage : IMessageEvent
{
    public required int ObjectId { get; init; }
    public required int Param { get; init; }
}
