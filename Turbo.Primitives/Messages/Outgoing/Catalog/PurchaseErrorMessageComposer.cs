using Orleans;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Catalog;

/// <summary>
/// The purchase alert the client shows from <c>catalog.alert.purchaseerror.description.{id}</c>.
/// It carries the enum, like its sibling <c>PurchaseNotAllowedMessageComposer</c>; the
/// serializer does the cast.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record PurchaseErrorMessageComposer : IComposer
{
    [Id(0)]
    public required CatalogPurchaseErrorType ErrorCode { get; init; }
}
