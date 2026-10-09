namespace Turbo.Catalog.Configuration;

/// <summary>How vouchers are redeemed and made.</summary>
public sealed class VoucherConfig
{
    /// <summary>
    /// Codes a player may get wrong in an hour before every code is refused them until the hour
    /// is up: guessing codes takes too long to be worth it.
    /// </summary>
    public int FailuresPerHour { get; init; } = 10;

    /// <summary>The random part of a generated code, in characters.</summary>
    public int GeneratedCodeLength { get; init; } = 10;

    /// <summary>Furniture one voucher gives at most.</summary>
    public int MaxFurnitureQuantity { get; init; } = 50;

    /// <summary>Vouchers made at once at most.</summary>
    public int GenerateMax { get; init; } = 1000;

    /// <summary>Vouchers a page of the panel's list holds.</summary>
    public int PageSize { get; init; } = 50;

    /// <summary>Redemptions of one voucher the panel lists.</summary>
    public int RedemptionsListed { get; init; } = 200;
}
