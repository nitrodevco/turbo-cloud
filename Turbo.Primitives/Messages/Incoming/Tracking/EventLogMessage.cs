using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Tracking;

/// <summary>
/// Something the player did that the client logs (<c>EventLogMessageComposer.as</c>): a category
/// such as <c>Navigation</c>, a type, an action, and an extra string and number whose meaning
/// depends on the event.
/// </summary>
public record EventLogMessage : IMessageEvent
{
    public required string Category { get; init; }
    public required string Type { get; init; }
    public required string Action { get; init; }
    public required string ExtraString { get; init; }
    public required int ExtraInt { get; init; }
}
