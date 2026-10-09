using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Catalog.Configuration;
using Turbo.Database.Context;
using Turbo.Database.Entities.Catalog;
using Turbo.Database.Extensions;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Providers;
using Turbo.Primitives.Players.Wallet;

namespace Turbo.Catalog.Vouchers;

/// <summary>
/// Vouchers (<see cref="IVoucherService"/>), kept in <c>vouchers</c> with each redemption in
/// <c>voucher_redemptions</c>. A redemption is one transaction: the voucher's uses are counted up
/// only while under its limit (<c>uses = uses + 1 where uses &lt; max</c>), and the redemption row's
/// unique index holds each player to one; a second try, or the last use taken by someone else,
/// rolls it all back. The rewards are given after: currencies under a reference of the voucher,
/// which the wallet credits once.
/// </summary>
public sealed partial class VoucherService(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IOptions<CatalogConfig> config,
    IGrainFactory grainFactory,
    ICurrencyTypeProvider currencies,
    TimeProvider time,
    ILogger<VoucherService> logger
) : IVoucherService
{
    /// <summary>
    /// What generated codes are made of: letters and digits, without the letters the client tells
    /// players codes never hold (i, l, o, w) or the digits that look like them.
    /// </summary>
    public const string CODE_ALPHABET = "ABCDEFGHJKMNPQRSTUVXYZ23456789";

    private readonly VoucherConfig _config = config.Value.Vouchers;

    /// <summary>When each player last got codes wrong, within the hour.</summary>
    private readonly ConcurrentDictionary<int, List<DateTimeOffset>> _failures = new();

    public async Task<VoucherRedeemResult> RedeemAsync(
        PlayerId player,
        string code,
        CancellationToken ct
    )
    {
        var now = time.GetUtcNow();

        if (TooManyFailures(player, now))
        {
            logger.LogWarning(
                "Player {PlayerId} tried a voucher while stopped for too many wrong codes",
                player.Value
            );

            return VoucherRedeemResult.Failed(VoucherRedeemErrorType.Invalid);
        }

        code = Normalize(code);

        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var voucher =
            code.Length == 0
                ? null
                : await dbCtx
                    .Vouchers.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Code == code, ct)
                    .ConfigureAwait(false);

        if (
            voucher is null
            || !voucher.Enabled
            || (voucher.ExpiresAt is { } expires && expires <= now.UtcDateTime)
        )
        {
            Fail(player, now);

            return VoucherRedeemResult.Failed(VoucherRedeemErrorType.Invalid);
        }

        if (!await TakeAsync(dbCtx, voucher.Id, player, ct).ConfigureAwait(false))
            return VoucherRedeemResult.Failed(VoucherRedeemErrorType.Invalid);

        logger.LogInformation(
            "Player {PlayerId} redeemed voucher {VoucherId} ({Code})",
            player.Value,
            voucher.Id,
            voucher.Code
        );

        return await GiveAsync(dbCtx, voucher.ToSnapshot(), player, ct).ConfigureAwait(false);
    }

    public async Task<(
        ImmutableArray<VoucherSnapshot> Vouchers,
        int Total,
        int PageSize
    )> SearchAsync(string? query, int page, CancellationToken ct)
    {
        var words = (query ?? string.Empty).Trim();
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var vouchers = dbCtx.Vouchers.AsNoTracking();

        if (words.Length > 0)
            vouchers = vouchers.Where(x => x.Code.Contains(words) || x.Note.Contains(words));

        var size = Math.Max(1, _config.PageSize);
        var total = await vouchers.CountAsync(ct).ConfigureAwait(false);
        var rows = await vouchers
            .OrderByDescending(x => x.Id)
            .Skip(Math.Max(0, page) * size)
            .Take(size)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return ([.. rows.Select(x => x.ToSnapshot())], total, size);
    }

    public async Task<VoucherSnapshot> SaveAsync(VoucherSnapshot voucher, CancellationToken ct)
    {
        var code = Normalize(voucher.Code);

        if (!CodePattern().IsMatch(code))
            throw new ArgumentException(
                $"A code is letters, digits, - and _, {VoucherEntity.CODE_MAX_LENGTH} at most.",
                nameof(voucher)
            );

        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        await CheckAsync(dbCtx, voucher, ct).ConfigureAwait(false);

        if (
            await dbCtx
                .Vouchers.AnyAsync(x => x.Code == code && x.Id != voucher.Id, ct)
                .ConfigureAwait(false)
        )
            throw new ArgumentException($"There is a voucher {code} already.", nameof(voucher));

        VoucherEntity row;

        if (voucher.Id == 0)
        {
            row = new VoucherEntity { Code = code, Note = "" };
            dbCtx.Vouchers.Add(row);
        }
        else
        {
            row =
                await dbCtx
                    .Vouchers.FirstOrDefaultAsync(x => x.Id == voucher.Id, ct)
                    .ConfigureAwait(false)
                ?? throw new ArgumentException(
                    $"There is no voucher {voucher.Id}.",
                    nameof(voucher)
                );

            if (voucher.MaxUses is { } max && max < row.Uses)
                throw new ArgumentException(
                    $"It has been redeemed {row.Uses} times already.",
                    nameof(voucher)
                );
        }

        row.Code = code;
        Apply(row, voucher);

        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

        logger.LogInformation("Voucher {VoucherId} ({Code}) saved", row.Id, row.Code);

        return row.ToSnapshot();
    }

    public async Task<ImmutableArray<VoucherSnapshot>> GenerateAsync(
        VoucherSnapshot template,
        int count,
        string prefix,
        CancellationToken ct
    )
    {
        prefix = Normalize(prefix);

        if (count < 1 || count > _config.GenerateMax)
            throw new ArgumentException(
                $"Make 1 to {_config.GenerateMax} vouchers at once.",
                nameof(count)
            );

        if (prefix.Length > 0 && !CodePattern().IsMatch(prefix))
            throw new ArgumentException("A prefix is letters, digits, - and _.", nameof(prefix));

        if (prefix.Length + _config.GeneratedCodeLength > VoucherEntity.CODE_MAX_LENGTH)
            throw new ArgumentException("The prefix is too long.", nameof(prefix));

        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        await CheckAsync(dbCtx, template, ct).ConfigureAwait(false);

        var codes = new HashSet<string>(StringComparer.Ordinal);

        while (codes.Count < count)
            codes.Add(
                prefix + RandomNumberGenerator.GetString(CODE_ALPHABET, _config.GeneratedCodeLength)
            );

        var taken = await dbCtx
            .Vouchers.Where(x => codes.Contains(x.Code))
            .Select(x => x.Code)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        // Thirty letters to the tenth: a clash is as good as never, but never isn't none.
        foreach (var code in taken)
        {
            codes.Remove(code);

            string another;

            do another =
                prefix
                + RandomNumberGenerator.GetString(CODE_ALPHABET, _config.GeneratedCodeLength);
            while (!codes.Add(another));
        }

        var rows = codes
            .Select(code =>
            {
                var row = new VoucherEntity { Code = code, Note = "" };

                Apply(row, template);

                return row;
            })
            .ToList();

        dbCtx.Vouchers.AddRange(rows);
        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

        logger.LogInformation(
            "{Count} vouchers made under the prefix {Prefix}",
            rows.Count,
            prefix
        );

        return [.. rows.Select(x => x.ToSnapshot())];
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var row = await dbCtx
            .Vouchers.FirstOrDefaultAsync(x => x.Id == id, ct)
            .ConfigureAwait(false);

        if (row is null)
            return false;

        var redemptions = await dbCtx
            .VoucherRedemptions.Where(x => x.VoucherEntityId == id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        dbCtx.VoucherRedemptions.RemoveRange(redemptions);
        dbCtx.Vouchers.Remove(row);
        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

        logger.LogInformation("Voucher {VoucherId} ({Code}) removed", id, row.Code);

        return true;
    }

    public async Task<IReadOnlyList<VoucherRedemptionSnapshot>> GetRedemptionsAsync(
        int id,
        CancellationToken ct
    )
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var rows = await dbCtx
            .VoucherRedemptions.AsNoTracking()
            .Where(x => x.VoucherEntityId == id)
            .OrderByDescending(x => x.Id)
            .Take(Math.Max(0, _config.RedemptionsListed))
            .Select(x => new
            {
                x.PlayerEntityId,
                x.PlayerEntity!.Name,
                x.CreatedAt,
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return
        [
            .. rows.Select(x => new VoucherRedemptionSnapshot
            {
                PlayerId = x.PlayerEntityId,
                PlayerName = x.Name,
                RedeemedAt = DateTime.SpecifyKind(x.CreatedAt, DateTimeKind.Utc),
            }),
        ];
    }

    /// <summary>
    /// Takes one of the voucher's uses for the player, or nothing: the uses counted up only while
    /// under the limit, and the player's redemption row added, in one transaction.
    /// </summary>
    private async Task<bool> TakeAsync(
        TurboDbContext dbCtx,
        int voucherId,
        PlayerId player,
        CancellationToken ct
    )
    {
        var tx = await dbCtx.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        await using var txScope = tx.ConfigureAwait(false);

        var taken = await dbCtx
            .Vouchers.Where(x => x.Id == voucherId && (x.MaxUses == null || x.Uses < x.MaxUses))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Uses, x => x.Uses + 1), ct)
            .ConfigureAwait(false);

        // Used up.
        if (taken == 0)
            return false;

        dbCtx.VoucherRedemptions.Add(
            new VoucherRedemptionEntity
            {
                VoucherEntityId = voucherId,
                PlayerEntityId = player.Value,
            }
        );

        try
        {
            await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            // Redeemed by this player before: the use goes back with the rollback.
            await tx.RollbackAsync(ct).ConfigureAwait(false);

            return false;
        }

        await tx.CommitAsync(ct).ConfigureAwait(false);

        return true;
    }

    /// <summary>
    /// Gives a redeemed voucher's rewards. Each is tried whatever became of the others; one that
    /// fails is logged with the voucher and the player, and the client told something went wrong.
    /// </summary>
    private async Task<VoucherRedeemResult> GiveAsync(
        TurboDbContext dbCtx,
        VoucherSnapshot voucher,
        PlayerId player,
        CancellationToken ct
    )
    {
        var failed = false;
        var wallet = grainFactory.GetPlayerWalletGrain(player);

        async Task TryAsync(string what, Func<Task<bool>> give)
        {
            try
            {
                if (await give().ConfigureAwait(false))
                    return;

                logger.LogError(
                    "Voucher {VoucherId}'s {What} could not be given to player {PlayerId}",
                    voucher.Id,
                    what,
                    player.Value
                );
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                logger.LogError(
                    ex,
                    "Giving voucher {VoucherId}'s {What} to player {PlayerId} failed",
                    voucher.Id,
                    what,
                    player.Value
                );
            }

            failed = true;
        }

        if (voucher.Credits > 0)
            await TryAsync(
                    "credits",
                    async () =>
                        await wallet
                            .CreditAsync(
                                CurrencyKind.Credits,
                                voucher.Credits,
                                Reference(voucher, "credits"),
                                ct
                            )
                            .ConfigureAwait(false)
                            is not WalletCreditResult.Rejected
                )
                .ConfigureAwait(false);

        if (
            voucher.CurrencyAmount > 0
            && voucher.CurrencyTypeId is { } typeId
            && currencies.GetCurrencyType(typeId) is { } currency
        )
            await TryAsync(
                    currency.Name,
                    async () =>
                        await wallet
                            .CreditAsync(
                                new CurrencyKind
                                {
                                    CurrencyType = currency.CurrencyType,
                                    ActivityPointType = currency.ActivityPointType,
                                },
                                voucher.CurrencyAmount,
                                Reference(voucher, "currency"),
                                ct
                            )
                            .ConfigureAwait(false)
                            is not WalletCreditResult.Rejected
                )
                .ConfigureAwait(false);

        if (voucher.BadgeCode is { Length: > 0 } badgeCode)
            await TryAsync(
                    "badge",
                    async () =>
                    {
                        // A badge the player has already is theirs either way.
                        await grainFactory
                            .GetPlayerBadgeGrain(player)
                            .GiveBadgeAsync(badgeCode, ct)
                            .ConfigureAwait(false);

                        return true;
                    }
                )
                .ConfigureAwait(false);

        var productName = string.Empty;
        var productDescription = string.Empty;

        if (voucher.FurnitureDefinitionId is { } definitionId && voucher.FurnitureQuantity > 0)
        {
            var inventory = grainFactory.GetInventoryGrain(player);

            for (var i = 0; i < voucher.FurnitureQuantity; i++)
                await TryAsync(
                        "furniture",
                        async () =>
                            await inventory
                                .GrantFurnitureAsync(definitionId, null, ct)
                                .ConfigureAwait(false)
                                is not null
                    )
                    .ConfigureAwait(false);

            var definition = await dbCtx
                .FurnitureDefinitions.AsNoTracking()
                .Where(x => x.Id == definitionId)
                .Select(x => new
                {
                    x.Name,
                    x.PublicName,
                    x.Description,
                })
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);

            productName = definition?.PublicName ?? definition?.Name ?? string.Empty;
            productDescription = definition?.Description ?? string.Empty;
        }

        return failed
            ? VoucherRedeemResult.Failed(VoucherRedeemErrorType.Technical)
            : new VoucherRedeemResult
            {
                ProductName = productName,
                ProductDescription = productDescription,
            };
    }

    /// <summary>A voucher's credit reference: the same for every try, so the wallet credits it once.</summary>
    private static string Reference(VoucherSnapshot voucher, string what) =>
        $"voucher:{voucher.Id}:{what}";

    /// <summary>Checks what a voucher gives and its limits. Throws <see cref="ArgumentException"/> for what can't be.</summary>
    private async Task CheckAsync(
        TurboDbContext dbCtx,
        VoucherSnapshot voucher,
        CancellationToken ct
    )
    {
        if (voucher.Credits < 0 || voucher.CurrencyAmount < 0 || voucher.FurnitureQuantity < 0)
            throw new ArgumentException("Amounts are 0 or more.", nameof(voucher));

        var furniture = voucher.FurnitureDefinitionId is not null && voucher.FurnitureQuantity > 0;
        var currency = voucher.CurrencyTypeId is not null && voucher.CurrencyAmount > 0;
        var badge = !string.IsNullOrWhiteSpace(voucher.BadgeCode);

        if (voucher.Credits == 0 && !currency && !furniture && !badge)
            throw new ArgumentException("A voucher gives something.", nameof(voucher));

        if (voucher.FurnitureQuantity > _config.MaxFurnitureQuantity)
            throw new ArgumentException(
                $"A voucher gives {_config.MaxFurnitureQuantity} furniture at most.",
                nameof(voucher)
            );

        if (
            voucher.FurnitureDefinitionId is { } definitionId
            && !await dbCtx
                .FurnitureDefinitions.AnyAsync(x => x.Id == definitionId, ct)
                .ConfigureAwait(false)
        )
            throw new ArgumentException($"There is no furniture {definitionId}.", nameof(voucher));

        if (voucher.CurrencyTypeId is { } typeId && currencies.GetCurrencyType(typeId) is null)
            throw new ArgumentException($"There is no currency {typeId}.", nameof(voucher));

        if (badge && !CodePattern().IsMatch(voucher.BadgeCode!.Trim()))
            throw new ArgumentException("A badge code is letters, digits and _.", nameof(voucher));

        if (voucher.MaxUses is < 1)
            throw new ArgumentException("A voucher is used once at least.", nameof(voucher));

        if ((voucher.Note ?? string.Empty).Length > VoucherEntity.NOTE_MAX_LENGTH)
            throw new ArgumentException("The note is too long.", nameof(voucher));
    }

    private static void Apply(VoucherEntity row, VoucherSnapshot voucher)
    {
        var furniture = voucher.FurnitureDefinitionId is not null && voucher.FurnitureQuantity > 0;
        var currency = voucher.CurrencyTypeId is not null && voucher.CurrencyAmount > 0;

        row.Credits = voucher.Credits;
        row.CurrencyTypeEntityId = currency ? voucher.CurrencyTypeId : null;
        row.CurrencyAmount = currency ? voucher.CurrencyAmount : 0;
        row.FurnitureDefinitionEntityId = furniture ? voucher.FurnitureDefinitionId : null;
        row.FurnitureQuantity = furniture ? voucher.FurnitureQuantity : 0;
        row.BadgeCode = string.IsNullOrWhiteSpace(voucher.BadgeCode)
            ? null
            : voucher.BadgeCode.Trim();
        row.MaxUses = voucher.MaxUses;
        row.ExpiresAt = voucher.ExpiresAt is { } expires
            ? DateTime.SpecifyKind(expires, DateTimeKind.Utc)
            : null;
        row.Enabled = voucher.Enabled;
        row.Note = (voucher.Note ?? string.Empty).Trim();
    }

    /// <summary>A code as it is kept: without spaces, in capitals.</summary>
    private static string Normalize(string code) =>
        string.Concat(code.Where(c => !char.IsWhiteSpace(c))).ToUpperInvariant();

    private bool TooManyFailures(PlayerId player, DateTimeOffset now)
    {
        if (!_failures.TryGetValue(player.Value, out var failures))
            return false;

        lock (failures)
        {
            failures.RemoveAll(x => now - x >= TimeSpan.FromHours(1));

            return failures.Count >= _config.FailuresPerHour;
        }
    }

    private void Fail(PlayerId player, DateTimeOffset now)
    {
        var failures = _failures.GetOrAdd(player.Value, _ => []);

        lock (failures)
            failures.Add(now);
    }

    [GeneratedRegex("^[A-Z0-9_-]{1,64}$", RegexOptions.IgnoreCase)]
    private static partial Regex CodePattern();
}
