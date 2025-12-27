using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;

[GenerateSerializer, Immutable]
public record UpdateSelectorMessage : UpdateWired, IMessageEvent
{
    [Id(0)]
    public required bool ResolveFilterField { get; init; }

    [Id(1)]
    public required bool ResolveInverseField { get; init; }
}
