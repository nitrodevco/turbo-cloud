using System;
using Turbo.Primitives.Catalog.Enums;

namespace Turbo.Catalog.Exceptions;

/// <summary>Raised when a catalog snapshot is requested for a catalog type that has no provider.</summary>
public sealed class CatalogTypeNotSupportedException(CatalogType catalogType)
    : Exception($"Catalog type '{catalogType}' is not supported.")
{
    public CatalogType CatalogType { get; } = catalogType;
}
