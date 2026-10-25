using System;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Catalog.Enums;

namespace Turbo.Catalog.Exceptions;

public sealed class CatalogPurchaseException(
    CatalogPurchaseErrorType errorType,
    CatalogBalanceFailure? balanceFailure = null,
    Exception? innerException = null,
    string? refusal = null
) : Exception($"Catalog purchase failed with error '{errorType}'.", innerException)
{
    public CatalogPurchaseErrorType ErrorType { get; } = errorType;

    public CatalogBalanceFailure? BalanceFailure { get; } = balanceFailure;

    /// <summary>A purchase policy's reason for refusing it, shown to the buyer in place of the hotel's own refusal.</summary>
    public string? Refusal { get; } = refusal;
}
