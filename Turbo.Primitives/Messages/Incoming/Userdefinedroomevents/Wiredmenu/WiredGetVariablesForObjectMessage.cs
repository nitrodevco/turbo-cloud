using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Userdefinedroomevents.Wiredmenu;

public record WiredGetVariablesForObjectMessage : IMessageEvent
{
    public required int SourceType { get; init; }
    public required int SourceId { get; init; }
}
