using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Context;
using Turbo.Database.Extensions;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Moderation.Enums;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Operations.Commands;

public sealed record AltsArguments(PlayerTarget Who);

/// <summary>
/// <c>:alts name</c>. The accounts that logged in from the player's addresses or machines, a
/// shared machine first: how a banned player's new account is found. Addresses themselves are
/// never shown, only how many are shared.
/// </summary>
[Command(
    "alts",
    Description = "List the accounts that share a player's addresses or machines",
    Category = CommandCategories.MODERATION
)]
[RequiresPermission(PermissionNodes.Command.ALTS)]
public sealed class AltsCommand(IDbContextFactory<TurboDbContext> dbCtxFactory)
    : IOperatorCommand<AltsArguments>
{
    public const int SHOWN = 15;

    private const string REPORTED = "reported";
    private const string NONE = "none";

    public IReadOnlyDictionary<string, string> DefaultTexts { get; } =
        new Dictionary<string, string>
        {
            [NONE] = "No other account shares an address or a machine with %0%.",
        };

    public async ValueTask<CommandResult> ExecuteAsync(
        IOperatorCommandContext ctx,
        AltsArguments arguments,
        CancellationToken ct
    )
    {
        var selection = await ctx.ResolveAsync(arguments.Who, ct);

        if (selection.Failure is { } failure)
            return failure;

        var target = selection.Players[0];

        await using var db = await dbCtxFactory.CreateDbContextAsync(ct);

        var alts = await db.FindAltsAsync(target.Id.Value, SHOWN, ct);

        if (alts.Count == 0)
            return CommandResult.Done(NONE, target.Name);

        var ids = alts.Select(x => x.PlayerId).ToList();
        var names = await db
            .Players.AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var now = DateTime.UtcNow;
        var banned = (
            await db
                .PlayerSanctions.AsNoTracking()
                .Where(x =>
                    ids.Contains(x.PlayerEntityId)
                    && x.Kind == SanctionKind.Ban
                    && x.RevokedAt == null
                    && (x.ExpiresAt == null || x.ExpiresAt > now)
                )
                .Select(x => x.PlayerEntityId)
                .ToListAsync(ct)
        ).ToHashSet();

        var lines = new List<string> { $"Accounts sharing with {target.Name}:" };

        foreach (var alt in alts)
        {
            var shares = new List<string>();

            if (alt.SharedMachine)
                shares.Add("same machine");

            if (alt.SharedAddresses > 0)
                shares.Add(
                    alt.SharedAddresses == 1
                        ? "1 address"
                        : $"{alt.SharedAddresses.ToString(CultureInfo.InvariantCulture)} addresses"
                );

            lines.Add(
                $"{names.GetValueOrDefault(alt.PlayerId, $"player {alt.PlayerId}")}: {string.Join(", ", shares)}, last {alt.LastSharedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}{(banned.Contains(alt.PlayerId) ? " (banned)" : string.Empty)}"
            );
        }

        await ctx.Executor.NoticeAsync(lines, ct);

        return CommandResult.Done(REPORTED);
    }
}
