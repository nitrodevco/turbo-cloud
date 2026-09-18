using System.Collections.Generic;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Messages.Incoming.Room.Engine;

public record SetObjectDataMessage : IMessageEvent
{
    public required RoomObjectId ObjectId { get; init; }
    public required Dictionary<string, string> Data { get; init; }
}
