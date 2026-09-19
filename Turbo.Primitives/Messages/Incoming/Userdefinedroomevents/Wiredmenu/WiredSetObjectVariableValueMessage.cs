using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Userdefinedroomevents.Wiredmenu;

public record WiredSetObjectVariableValueMessage : IMessageEvent
{
    public required int VariableTarget { get; init; }
    public required int ObjectIdForType { get; init; }
    public required string VariableId { get; init; }
    public required int Value { get; init; }
    public required int ReferenceRoomId { get; init; }
}
