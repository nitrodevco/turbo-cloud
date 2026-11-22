using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Navigator;

namespace Turbo.Import.Morningstar.Steps;

/// <summary>
/// The categories a player files a room under (<c>navigator_flatcats</c>). Both hotels number
/// their own, so they are matched by name: a Morningstar category the target has by name (in any
/// case) is that one, and one it lacks is added. A room keeps its category through the map this
/// fills.
/// </summary>
internal sealed class NavigatorCategoriesStep : IImportStep
{
    public const string TABLE = "navigator_flatcats";

    public string Name => "navigator categories";

    public async Task RunAsync(ImportContext ctx, CancellationToken ct)
    {
        var existing = await ctx
            .Db.NavigatorFlatCategories.AsNoTracking()
            .Select(x => new { x.Id, x.Name })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var byName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var category in existing)
            byName.TryAdd(category.Name.Trim(), category.Id);

        var nextId = existing.Count == 0 ? 1 : existing.Max(x => x.Id) + 1;

        await foreach (var row in ctx.Source.ReadAsync(TABLE, "id", ct).ConfigureAwait(false))
        {
            var sourceId = row.Int("id");
            var name = row.Text("caption").Trim();

            if (name.Length == 0)
                name = row.Text("caption_save", $"Category {sourceId}").Trim();

            if (byName.TryGetValue(name, out var id))
            {
                ctx.FlatCategories[sourceId] = id;

                continue;
            }

            // navigator_flatcats takes its ids from the hotel, not from the database.
            var entity = new NavigatorFlatCategoryEntity
            {
                Id = nextId++,
                Name = name,
                Visible = true,
                Automatic = false,
                StaffOnly = row.Int("min_rank", 1) > 1,
                MinRank = Math.Max(1, row.Int("min_rank", 1)),
                OrderNum = row.Int("order_num"),
            };

            await ctx.AddSetupAsync(entity, TABLE, ct).ConfigureAwait(false);

            byName[name] = entity.Id;
            ctx.FlatCategories[sourceId] = entity.Id;
        }
    }
}
