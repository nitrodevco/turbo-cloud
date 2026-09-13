using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;

namespace Turbo.Primitives.Messages.Incoming.Room.Action;

public record MuteUserMessage : IMessageEvent
{
    public required PlayerId PlayerId { get; init; }
    public required RoomId RoomId { get; init; }
    public required int DurationInMinutes { get; init; }
}
