using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Habbicons;

public record TriggerHabbiconMessage : IMessageEvent
{
    public required int HabbiconId { get; init; }
}
