using Turbo.Contracts.Abstractions;
using Turbo.Contracts.Enums.Catalog;

namespace Turbo.Primitives.Messages.Incoming.Catalog;

public record GetCatalogIndexMessage : IMessageEvent
{
    public required CatalogTypeEnum CatalogType { get; init; }
}
