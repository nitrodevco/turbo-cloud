using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Incoming.Room.Engine;

public record UseWallItemMessage : IMessageEvent
{
    public required int ObjectId { get; init; }
    public required int Param { get; init; }
}
