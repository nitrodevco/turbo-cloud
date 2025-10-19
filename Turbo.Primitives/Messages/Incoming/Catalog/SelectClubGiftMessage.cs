using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Incoming.Catalog;

public record SelectClubGiftMessage : IMessageEvent
{
    public string? ProductCode { get; init; }
}
