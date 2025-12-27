using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;

[GenerateSerializer, Immutable]
public record UpdateActionMessage : UpdateWired, IMessageEvent
{
    [Id(0)]
    public required int ActionDelay { get; init; }
}
