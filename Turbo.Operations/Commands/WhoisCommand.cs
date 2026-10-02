using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Providers;

namespace Turbo.Operations.Commands;

public sealed record WhoisArguments(PlayerTarget Who);

/// <summary>
/// <c>:whois name</c>. What staff ask first about a player: whether they are here and where, the
/// groups they hold, any ban, whether they are silenced or trade locked, and what is in their
/// wallet. A report, not a status, so it is a notice rather than a whisper.
/// </summary>
[Command("whois", Description = "Look up a player", Category = CommandCategories.SUPPORT)]
[RequiresPermission(PermissionNodes.Command.WHOIS)]
public sealed class WhoisCommand(
    IGrainFactory grainFactory,
    ISessionGateway sessionGateway,
    ISanctionService sanctionService,
    ICurrencyTypeProvider currencyTypeProvider
) : IOperatorCommand<WhoisArguments>
{
    private const string REPORTED = "reported";

    private const string DATE_FORMAT = "yyyy-MM-dd HH:mm 'UTC'";

    public async ValueTask<CommandResult> ExecuteAsync(
        IOperatorCommandContext ctx,
        WhoisArguments arguments,
        CancellationToken ct
    )
    {
        var selection = await ctx.ResolveAsync(arguments.Who, ct);

        if (selection.Failure is { } failure)
            return failure;

        var target = selection.Players[0];
        var summary = await grainFactory.GetPlayerGrain(target.Id).GetSummaryAsync(ct);
        var resolved = await grainFactory.GetPlayerPermissionGrain(target.Id).GetResolvedAsync(ct);
        var ban = await sanctionService.GetActiveBanAsync(target.Id, ct);
        var online = sessionGateway.GetOnlinePlayerIds().Contains(target.Id);

        var lines = new List<string>
        {
            $"{target.Name} (player {target.Id})",
            $"Registered: {summary.CreatedAt.ToString(DATE_FORMAT, CultureInfo.InvariantCulture)}",
            await WhereAsync(target.Id, online, summary.LastUpdated, ct),
            $"Groups: {GroupsOf(resolved)}",
            ban is null
                ? "Ban: none"
                : $"Ban: {(ban.ExpiresAtUtc is { } ends ? $"until {ends.ToString(DATE_FORMAT, CultureInfo.InvariantCulture)}" : "permanent")} ({ban.Reason})",
            $"Silenced: {Yes(!resolved.Has(PermissionNodes.Chat.SPEAK))}, trade locked: {Yes(!resolved.Has(PermissionNodes.TRADE))}",
            $"Wallet: {await WalletAsync(target.Id, ct)}",
        };

        await ctx.Executor.NoticeAsync(lines, ct);

        return CommandResult.Done(REPORTED);
    }

    private async Task<string> WhereAsync(
        PlayerId playerId,
        bool online,
        DateTime lastSeenUtc,
        CancellationToken ct
    )
    {
        if (!online)
            return $"Offline since {lastSeenUtc.ToString(DATE_FORMAT, CultureInfo.InvariantCulture)}";

        var room = await grainFactory.GetPlayerPresenceGrain(playerId).GetActiveRoomAsync(ct);

        if (room.RoomId.Value <= 0)
            return "Online, in no room";

        var summary = await grainFactory.GetRoomGrain(room.RoomId).GetSummaryAsync(ct);

        return $"Online, in room {room.RoomId} \"{summary.Name}\"";
    }

    /// <summary>The groups a player holds, through membership nodes, inherited ones too.</summary>
    private static string GroupsOf(
        Turbo.Primitives.Players.Snapshots.Permissions.ResolvedPermissionsSnapshot resolved
    )
    {
        var prefix = PermissionGroupNames.ToNode(string.Empty);

        var groups = resolved
            .Granted.Where(node =>
                node.StartsWith(prefix, StringComparison.Ordinal)
                && node.Length > prefix.Length
                && node[prefix.Length..] != PermissionGroupNames.DEFAULT
            )
            .Select(node => node[prefix.Length..])
            .Order(StringComparer.Ordinal)
            .ToList();

        return groups.Count == 0 ? "none" : string.Join(", ", groups);
    }

    private async Task<string> WalletAsync(PlayerId playerId, CancellationToken ct)
    {
        var wallet = grainFactory.GetPlayerWalletGrain(playerId);
        var balances = new List<string>();

        foreach (var name in currencyTypeProvider.GetEnabledCurrencyNames().Order())
            if (currencyTypeProvider.TryGetCurrencyKindByName(name, out var kind))
                balances.Add($"{name} {await wallet.GetAmountForCurrencyAsync(kind, ct)}");

        return balances.Count == 0 ? "nothing" : string.Join(", ", balances);
    }

    private static string Yes(bool value) => value ? "yes" : "no";
}
