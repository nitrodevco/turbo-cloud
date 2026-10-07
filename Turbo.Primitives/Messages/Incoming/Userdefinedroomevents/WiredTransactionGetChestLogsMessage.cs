using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;

/// <summary>The user asks for a page of one wired chest's transaction log.</summary>
public record WiredTransactionGetChestLogsMessage : IMessageEvent
{
    public required int ChestId { get; init; }
    public required int PageSize { get; init; }
    public required int Page { get; init; }
}
