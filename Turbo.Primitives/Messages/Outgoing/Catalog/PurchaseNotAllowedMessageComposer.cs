using Orleans;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Catalog;

[GenerateSerializer, Immutable]
public sealed record PurchaseNotAllowedMessageComposer : IComposer
{
    [Id(0)]
    public required CatalogPurchaseErrorType ErrorType { get; init; }
}
