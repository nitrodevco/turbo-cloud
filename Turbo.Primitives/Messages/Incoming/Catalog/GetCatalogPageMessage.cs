using Turbo.Contracts.Abstractions;
using Turbo.Contracts.Enums.Catalog;

namespace Turbo.Primitives.Messages.Incoming.Catalog;

public record GetCatalogPageMessage : IMessageEvent
{
    public int PageId { get; init; }
    public int OfferId { get; init; }
    public required CatalogTypeEnum CatalogType { get; init; }
}
