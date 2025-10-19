using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Incoming.Tracking;

public record EventLogMessage : IMessageEvent
{
    public required string Event { get; init; }
    public required string Data { get; init; }
    public required string Action { get; init; }
    public required string ExtraString { get; init; }
    public required int ExtraInt { get; init; }
}
