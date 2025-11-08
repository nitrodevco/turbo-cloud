using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Incoming.Room.Session;

public record OpenFlatConnectionMessage : IMessageEvent
{
    public int RoomId { get; init; }
    public string Password { get; init; } = string.Empty;
    public int Unknown1 { get; init; }
}
