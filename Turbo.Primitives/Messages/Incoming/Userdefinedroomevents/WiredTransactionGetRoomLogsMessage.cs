using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;

/// <summary>The user asks for a page of the current room's wired transaction log.</summary>
public record WiredTransactionGetRoomLogsMessage : IMessageEvent
{
    public required int PageSize { get; init; }
    public required int Page { get; init; }
}
