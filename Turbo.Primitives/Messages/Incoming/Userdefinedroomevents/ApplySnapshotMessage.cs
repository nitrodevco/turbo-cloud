using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;

public record ApplySnapshotMessage : IMessageEvent
{
    public required int Id { get; init; }
}
