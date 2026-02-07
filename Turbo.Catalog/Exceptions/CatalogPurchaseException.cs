using System;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Catalog.Enums;

namespace Turbo.Catalog.Exceptions;

public sealed class CatalogPurchaseException(
    CatalogPurchaseErrorType errorType,
    CatalogBalanceFailure? balanceFailure = null,
    Exception? innerException = null
) : Exception($"Catalog purchase failed with error '{errorType}'.", innerException)
{
    public CatalogPurchaseErrorType ErrorType { get; } = errorType;

    public CatalogBalanceFailure? BalanceFailure { get; } = balanceFailure;
}
