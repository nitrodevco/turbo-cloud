using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;

public record UpdateSelectorMessage : UpdateWired, IMessageEvent
{
    public required bool ResolveFilterField { get; init; }
    public required bool ResolveInverseField { get; init; }
}
