using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Turbo.Import.Morningstar.Steps;

/// <summary>
/// Refuses a target that already has players, rooms, furniture, pets, bots or groups: the import
/// keeps Morningstar's ids, which would collide with them, and a hotel merged into another is not
/// something it can do without losing one side's rows.
/// </summary>
internal sealed class PreflightStep : IImportStep
{
    public string Name => "preflight";

    public async Task RunAsync(ImportContext ctx, CancellationToken ct)
    {
        var db = ctx.Db;
        var taken = new List<string>();

        if (await db.Players.AnyAsync(ct).ConfigureAwait(false))
            taken.Add("players");

        if (await db.Rooms.AnyAsync(ct).ConfigureAwait(false))
            taken.Add("rooms");

        if (await db.Furnitures.AnyAsync(ct).ConfigureAwait(false))
            taken.Add("furniture");

        if (await db.Pets.AnyAsync(ct).ConfigureAwait(false))
            taken.Add("pets");

        if (await db.Bots.AnyAsync(ct).ConfigureAwait(false))
            taken.Add("bots");

        if (await db.Guilds.AnyAsync(ct).ConfigureAwait(false))
            taken.Add("guilds");

        if (taken.Count > 0)
            throw new ImportException(
                $"The target database already has {string.Join(", ", taken)}. An import keeps "
                    + "Morningstar's ids, so it needs a database without them: stop the server, back it "
                    + "up, and run scripts/sql/fresh-start.sql on it first (it keeps the hotel's setup)."
            );

        ctx.Log.LogInformation(
            "Reading {Source} ({Users:N0} users, {Rooms:N0} rooms, {Items:N0} items){DryRun}",
            ctx.Source.Name,
            await ctx.Source.CountAsync("users", ct).ConfigureAwait(false),
            await ctx.Source.CountAsync("rooms", ct).ConfigureAwait(false),
            await ctx.Source.CountAsync("items", ct).ConfigureAwait(false),
            ctx.Options.DryRun ? "; dry run, nothing is written" : string.Empty
        );
    }
}
