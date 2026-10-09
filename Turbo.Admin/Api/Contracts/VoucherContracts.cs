using System;
using Turbo.Primitives.Catalog.Snapshots;

namespace Turbo.Admin.Api.Contracts;

/// <summary>A voucher as staff write it; the expiry is UTC.</summary>
public sealed record VoucherRequest(
    string? Code = null,
    int? Credits = null,
    int? CurrencyTypeId = null,
    int? CurrencyAmount = null,
    int? FurnitureDefinitionId = null,
    int? FurnitureQuantity = null,
    string? BadgeCode = null,
    int? MaxUses = null,
    DateTime? ExpiresAt = null,
    bool? Enabled = null,
    string? Note = null
);

/// <summary>Vouchers to make at once: how many, the prefix of their codes, and what each gives.</summary>
public sealed record VoucherGenerateRequest(int? Count, string? Prefix, VoucherRequest? Voucher);

/// <summary>A voucher as the panel lists it, with its furniture's name.</summary>
public sealed record VoucherItem(VoucherSnapshot Voucher, string? FurnitureName);
