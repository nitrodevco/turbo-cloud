using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Habbicons;

public record ClaimHabbiconMessage : IMessageEvent
{
    public required int HabbiconId { get; init; }
}
