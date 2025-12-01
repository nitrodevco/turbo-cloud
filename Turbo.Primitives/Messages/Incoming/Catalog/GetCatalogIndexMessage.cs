using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Catalog;

public record GetCatalogIndexMessage : IMessageEvent
{
    public required CatalogType CatalogType { get; init; }
}
