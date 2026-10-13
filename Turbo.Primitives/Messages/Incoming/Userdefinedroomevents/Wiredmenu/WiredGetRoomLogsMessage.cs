using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Userdefinedroomevents.Wiredmenu;

/// <summary>
/// The user asks for a page of the room's wired log (AS3 <c>WiredGetRoomLogsComposer</c>: page,
/// page size, level filter, source filter, text; -1 is no filter).
/// </summary>
public record WiredGetRoomLogsMessage : IMessageEvent
{
    public required int Page { get; init; }
    public required int PageSize { get; init; }
    public required int LogLevelFilter { get; init; }
    public required int LogSourceFilter { get; init; }
    public required string Query { get; init; }
}
