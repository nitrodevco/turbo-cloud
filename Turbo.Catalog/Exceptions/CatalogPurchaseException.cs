using System;
using Turbo.Primitives.Catalog.Enums;

namespace Turbo.Catalog.Exceptions;

public sealed class CatalogPurchaseException(CatalogPurchaseErrorType errorType) : Exception
{
    public CatalogPurchaseErrorType ErrorType { get; } = errorType;
}
