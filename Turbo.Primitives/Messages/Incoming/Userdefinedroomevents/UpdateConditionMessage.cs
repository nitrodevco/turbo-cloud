using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;

[GenerateSerializer, Immutable]
public record UpdateConditionMessage : UpdateWired, IMessageEvent
{
    [Id(0)]
    public required int Quantifier { get; init; }
}
