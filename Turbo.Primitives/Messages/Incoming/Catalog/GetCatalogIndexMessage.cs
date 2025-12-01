using Turbo.Contracts.Abstractions;
using Turbo.Primitives.Catalog.Enums;

namespace Turbo.Primitives.Messages.Incoming.Catalog;

public record GetCatalogIndexMessage : IMessageEvent
{
    public required CatalogType CatalogType { get; init; }
}
