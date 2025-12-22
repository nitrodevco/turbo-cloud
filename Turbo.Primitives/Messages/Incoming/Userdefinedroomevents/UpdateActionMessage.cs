using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;

public record UpdateActionMessage : UpdateWired, IMessageEvent
{
    public required int ActionDelay { get; init; }
}
