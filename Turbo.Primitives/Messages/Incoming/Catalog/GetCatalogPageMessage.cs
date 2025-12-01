using Turbo.Contracts.Abstractions;
using Turbo.Primitives.Catalog.Enums;

namespace Turbo.Primitives.Messages.Incoming.Catalog;

public record GetCatalogPageMessage : IMessageEvent
{
    public int PageId { get; init; }
    public int OfferId { get; init; }
    public required CatalogType CatalogType { get; init; }
}
