using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Incoming.Catalog;

public record GetCatalogIndexMessage : IMessageEvent
{
    public required string Type { get; init; }
}
