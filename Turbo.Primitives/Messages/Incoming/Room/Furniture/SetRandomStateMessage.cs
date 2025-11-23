using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Incoming.Room.Furniture;

public record SetRandomStateMessage : IMessageEvent
{
    public required int ObjectId { get; init; }
    public required int Param { get; init; }
}
