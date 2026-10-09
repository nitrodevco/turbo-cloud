using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Catalog;

/// <summary>
/// Vouchers: codes players redeem in the catalogue for credits, another currency, furniture or a
/// badge, and the managing staff do in the panel. Each player redeems a voucher once, a voucher's
/// uses are never exceeded whichever silo redeems it, and a player who keeps typing wrong codes is
/// stopped for a while.
/// </summary>
public interface IVoucherService
{
    /// <summary>Redeems a code for the player and gives its rewards; or why not.</summary>
    public Task<VoucherRedeemResult> RedeemAsync(
        PlayerId player,
        string code,
        CancellationToken ct
    );

    /// <summary>Vouchers whose code or note holds the words, newest first: a page of them, how many in all, and how many a page holds.</summary>
    public Task<(ImmutableArray<VoucherSnapshot> Vouchers, int Total, int PageSize)> SearchAsync(
        string? query,
        int page,
        CancellationToken ct
    );

    /// <summary>
    /// Adds a voucher (id 0) or changes one. Throws <see cref="System.ArgumentException"/> for a code
    /// that is empty, taken or holds more than letters and digits, a voucher that gives nothing,
    /// furniture, a currency or a badge there isn't, or limits below what it has been used.
    /// </summary>
    public Task<VoucherSnapshot> SaveAsync(VoucherSnapshot voucher, CancellationToken ct);

    /// <summary>
    /// Makes <paramref name="count"/> vouchers like <paramref name="template"/>, each its own random
    /// code after <paramref name="prefix"/>, none with the letters players are told codes never hold.
    /// </summary>
    public Task<ImmutableArray<VoucherSnapshot>> GenerateAsync(
        VoucherSnapshot template,
        int count,
        string prefix,
        CancellationToken ct
    );

    /// <summary>Removes a voucher and its redemptions; false when there is none.</summary>
    public Task<bool> DeleteAsync(int id, CancellationToken ct);

    /// <summary>Who redeemed a voucher, newest first, up to the configured count.</summary>
    public Task<IReadOnlyList<VoucherRedemptionSnapshot>> GetRedemptionsAsync(
        int id,
        CancellationToken ct
    );
}
