using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;

public record OpenMessage : IMessageEvent
{
    public required int Id { get; init; }
}
