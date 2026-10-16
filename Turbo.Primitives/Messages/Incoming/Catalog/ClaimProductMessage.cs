using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Catalog;

public record ClaimProductMessage : IMessageEvent
{
    public required string ClaimId { get; init; }

    /// <summary>The option picked (the adoption window's 0 or 1); -1 when the client sent none.</summary>
    public required int Choice { get; init; }
}
