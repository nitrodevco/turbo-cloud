using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;

public record UpdateConditionMessage : UpdateWired, IMessageEvent
{
    public required int Quantifier { get; init; }
}
