using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Incoming.Room.Engine;

public record UseFurnitureMessage : IMessageEvent
{
    public required int ObjectId { get; init; }
    public required int Param { get; init; }
}
