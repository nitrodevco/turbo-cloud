namespace Turbo.Primitives.Catalog.Enums;

/// <summary>
/// Why a voucher was not redeemed, as the client's <c>catalog.alert.voucherredeem.error.description.&lt;code&gt;</c>
/// texts number it.
/// </summary>
public enum VoucherRedeemErrorType
{
    /// <summary>Not a code that can be redeemed: unknown, used up, expired, turned off, or already redeemed by the player.</summary>
    Invalid = 0,

    /// <summary>Something went wrong redeeming it.</summary>
    Technical = 1,

    /// <summary>The code is redeemed on the hotel's website, not in the client.</summary>
    WebOnly = 3,
}
