using Turbo.Primitives.Networking;
using Turbo.Primitives.Pets.Enums;

namespace Turbo.Primitives.Messages.Incoming.Room.Engine;

public record GiveSupplementToPetMessage : IMessageEvent
{
    public required int PetId { get; init; }
    public required PetSupplementType Supplement { get; init; }
}
