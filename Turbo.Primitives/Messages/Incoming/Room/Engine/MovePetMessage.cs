using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Messages.Incoming.Room.Engine;

/// <summary>Move or turn a placed monsterplant.</summary>
public record MovePetMessage : IMessageEvent
{
    public required int PetId { get; init; }
    public required int X { get; init; }
    public required int Y { get; init; }
    public required Rotation Rotation { get; init; }
}
