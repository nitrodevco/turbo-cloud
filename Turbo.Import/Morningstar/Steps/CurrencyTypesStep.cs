using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Catalog;
using Turbo.Primitives.Players.Enums.Wallet;

namespace Turbo.Import.Morningstar.Steps;

/// <summary>
/// The currencies players hold: credits, and each activity point type in
/// <c>users_currency</c> (0 duckets, 5 diamonds, seasonal types above 100). A currency the target
/// already has (credits by kind, points by their type number) is used as it is; one it lacks is
/// added, enabled.
/// </summary>
internal sealed class CurrencyTypesStep : IImportStep
{
    public const string TABLE = "currency_types";

    // Morningstar's first seasonal currency.
    private const int SEASONAL = 101;

    private static readonly Dictionary<int, string> NAMES = new()
    {
        [(int)ActivityPointType.Duckets] = "duckets",
        [(int)ActivityPointType.Diamonds] = "diamonds",
        [SEASONAL] = "seasonal",
    };

    public string Name => "currencies";

    public async Task RunAsync(ImportContext ctx, CancellationToken ct)
    {
        var existing = await ctx
            .Db.CurrencyTypes.AsNoTracking()
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var credits = existing.FirstOrDefault(x => x.CurrencyType == CurrencyType.Credits);

        ctx.CurrencyTypes[ImportContext.CREDITS] = credits?.Id
            ?? await ctx.AddSetupAsync(
                    new CurrencyTypeEntity
                    {
                        Name = "credits",
                        CurrencyType = CurrencyType.Credits,
                        Enabled = true,
                    },
                    TABLE,
                    ct
                )
                .ConfigureAwait(false);

        // Duckets and diamonds every hotel has, held or not (the SeedCurrencyTypes migration
        // gives a migrated hotel both); then whatever else players hold.
        var types = new SortedSet<int>
        {
            (int)ActivityPointType.Duckets,
            (int)ActivityPointType.Diamonds,
        };

        foreach (
            var type in await ctx
                .Source.DistinctAsync("users_currency", "type", ct)
                .ConfigureAwait(false)
        )
            if (int.TryParse(type, out var parsed))
                types.Add(parsed);

        foreach (var type in types)
        {
            var match = existing.FirstOrDefault(x =>
                x.CurrencyType == CurrencyType.ActivityPoints && x.ActivityPointType == type
            );

            ctx.CurrencyTypes[type] = match?.Id
                ?? await ctx.AddSetupAsync(
                        new CurrencyTypeEntity
                        {
                            Name = NAMES.GetValueOrDefault(type, $"points_{type}"),
                            CurrencyType = CurrencyType.ActivityPoints,
                            ActivityPointType = type,
                            Enabled = true,
                        },
                        TABLE,
                        ct
                    )
                    .ConfigureAwait(false);
        }
    }
}
