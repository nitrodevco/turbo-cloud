using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Grains.Permissions;
using Turbo.Primitives.Players.Snapshots.Permissions;

namespace Turbo.Main.Console;

/// <summary>
/// The <c>perm</c> console command: reads and edits groups and players' permissions through the
/// permission grains, as the console (audited with no actor). See <c>docs/permissions.md</c>.
/// </summary>
internal sealed class PermissionConsoleCommand(IGrainFactory grainFactory)
{
    public const string USAGE = """
            perm check <player> <node>                         why a player does or does not hold a node
            perm user <player> info                            groups, nodes, meta and resolved set
            perm user <player> audit [count]
            perm user <player> group add <group> [duration] [--extend]
            perm user <player> group remove|removetemp <group>
            perm user <player> set <node> [true|false] [duration] [--extend]
            perm user <player> unset|unsettemp <node>
            perm user <player> meta set <key> <value> [duration] [--extend]
            perm user <player> meta unset|unsettemp <key>
            perm groups
            perm group <group> info
            perm group <group> audit [count]
            perm group <group> create [weight] [display name]
            perm group <group> delete
            perm group <group> weight <weight>
            perm group <group> rename <display name>
            perm group <group> set <node> [true|false] [duration] [--extend]
            perm group <group> unset|unsettemp <node>
            perm group <group> meta set <key> <value> [duration] [--extend]
            perm group <group> meta unset|unsettemp <key>
            perm group <group> parent add|remove <parent>
          durations: 30s, 15m, 12h, 7d, 2w; left out, permanent. A temporary assignment sits beside
          a permanent one of the same node and wins while it lasts; unsettemp removes it, unset the
          permanent one. --extend adds the duration to one already running instead of replacing it.
        """;

    private const int DEFAULT_AUDIT_COUNT = 20;

    private const string EXTEND_FLAG = "--extend";

    private readonly IGrainFactory _grainFactory = grainFactory;

    /// <summary>What a temporary write does to one already running; <c>--extend</c> anywhere asks to extend.</summary>
    private PermissionExpiryModeType _mode;

    private IPermissionGroupDirectoryGrain Directory =>
        _grainFactory.GetPermissionGroupDirectoryGrain();

    public async Task RunAsync(string[] args, CancellationToken ct)
    {
        _mode = args.Contains(EXTEND_FLAG)
            ? PermissionExpiryModeType.Extend
            : PermissionExpiryModeType.Replace;
        args = [.. args.Where(x => x != EXTEND_FLAG)];

        try
        {
            var handled = args switch
            {
                ["check", var player, var node] => await CheckAsync(player, node, ct)
                    .ConfigureAwait(false),
                ["groups"] => await ListGroupsAsync(ct).ConfigureAwait(false),
                ["user", var player, .. var rest] => await UserAsync(player, rest, ct)
                    .ConfigureAwait(false),
                ["group", var group, .. var rest] => await GroupAsync(group, rest, ct)
                    .ConfigureAwait(false),
                _ => false,
            };

            if (!handled)
                System.Console.WriteLine(USAGE);
        }
        catch (FormatException ex)
        {
            System.Console.WriteLine(ex.Message);
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"perm failed: {ex.Message}");
        }
    }

    private async Task<bool> CheckAsync(string playerName, string node, CancellationToken ct)
    {
        if (await FindPlayerAsync(playerName, ct).ConfigureAwait(false) is not { } playerId)
            return true;

        var check = await _grainFactory
            .GetPlayerPermissionGrain(playerId)
            .ExplainAsync(node, ct)
            .ConfigureAwait(false);

        System.Console.WriteLine(
            $"{node} = {Format(check.Granted)}{(check.IsRegistered ? "" : "  (not registered)")}"
        );

        if (check.Decision is null)
            System.Console.WriteLine("  decided by  nothing: denied by default");
        else
        {
            System.Console.WriteLine($"  decided by  {Describe(check.Decision)}");

            if (check.Decision.Path.Length > 0)
                System.Console.WriteLine(
                    $"  path        {playerName} -> {string.Join(" -> ", check.Decision.Path)}"
                );
        }

        foreach (var overridden in check.Overridden)
            System.Console.WriteLine($"  overrides   {Describe(overridden)}");

        return true;
    }

    private async Task<bool> ListGroupsAsync(CancellationToken ct)
    {
        var snapshot = await Directory.GetSnapshotAsync(ct).ConfigureAwait(false);

        foreach (var group in snapshot.Groups.Values.OrderByDescending(x => x.Weight))
        {
            var parents = group.ParentIds.Select(id =>
                snapshot.Groups.TryGetValue(id, out var parent) ? parent.Name : $"#{id}"
            );

            System.Console.WriteLine(
                $"{group.Name, -16} weight {group.Weight, 5}  {group.Nodes.Length, 3} nodes  "
                    + $"parents: {OrDash(string.Join(", ", parents))}  \"{group.DisplayName}\""
            );
        }

        return true;
    }

    private async Task<bool> UserAsync(string playerName, string[] args, CancellationToken ct)
    {
        if (await FindPlayerAsync(playerName, ct).ConfigureAwait(false) is not { } playerId)
            return true;

        var grain = _grainFactory.GetPlayerPermissionGrain(playerId);

        switch (args)
        {
            case ["info"]:
                await PrintUserAsync(grain, ct).ConfigureAwait(false);
                return true;
            case ["audit"]:
                PrintAudit(
                    await grain.GetAuditAsync(DEFAULT_AUDIT_COUNT, ct).ConfigureAwait(false)
                );
                return true;
            case ["audit", var count]:
                PrintAudit(await grain.GetAuditAsync(ParseInt(count), ct).ConfigureAwait(false));
                return true;
            case ["group", "add", var group, .. var rest]:
                Report(
                    await grain
                        .AddGroupAsync(group, ParseExpiry(rest), _mode, null, ct)
                        .ConfigureAwait(false)
                );
                return true;
            case ["group", "remove" or "removetemp", var group]:
                Report(
                    await grain
                        .RemoveGroupAsync(group, args[1] == "removetemp", null, ct)
                        .ConfigureAwait(false)
                );
                return true;
            case ["set", var node, .. var rest]:
            {
                var (value, expiresAt) = ParseValueAndExpiry(rest);
                Report(
                    await grain
                        .SetNodeAsync(node, value, expiresAt, _mode, null, ct)
                        .ConfigureAwait(false)
                );
                return true;
            }
            case ["unset" or "unsettemp", var node]:
                Report(
                    await grain
                        .UnsetNodeAsync(node, args[0] == "unsettemp", null, ct)
                        .ConfigureAwait(false)
                );
                return true;
            case ["meta", "set", var key, var value, .. var rest]:
                Report(
                    await grain
                        .SetMetaAsync(key, value, ParseExpiry(rest), _mode, null, ct)
                        .ConfigureAwait(false)
                );
                return true;
            case ["meta", "unset" or "unsettemp", var key]:
                Report(
                    await grain
                        .UnsetMetaAsync(key, args[1] == "unsettemp", null, ct)
                        .ConfigureAwait(false)
                );
                return true;
            default:
                return false;
        }
    }

    private async Task<bool> GroupAsync(string group, string[] args, CancellationToken ct)
    {
        var directory = Directory;

        switch (args)
        {
            case ["info"]:
                await PrintGroupAsync(group, ct).ConfigureAwait(false);
                return true;
            case ["audit"]:
                PrintAudit(
                    await directory
                        .GetAuditAsync(group, DEFAULT_AUDIT_COUNT, ct)
                        .ConfigureAwait(false)
                );
                return true;
            case ["audit", var count]:
                PrintAudit(
                    await directory.GetAuditAsync(group, ParseInt(count), ct).ConfigureAwait(false)
                );
                return true;
            case ["create"]:
                Report(
                    await directory
                        .CreateGroupAsync(group, group, 0, null, ct)
                        .ConfigureAwait(false)
                );
                return true;
            case ["create", var weight, .. var displayName]:
                Report(
                    await directory
                        .CreateGroupAsync(
                            group,
                            displayName.Length > 0 ? string.Join(' ', displayName) : group,
                            ParseInt(weight),
                            null,
                            ct
                        )
                        .ConfigureAwait(false)
                );
                return true;
            case ["delete"]:
                Report(await directory.DeleteGroupAsync(group, null, ct).ConfigureAwait(false));
                return true;
            case ["weight", var weight]:
                Report(
                    await directory
                        .SetWeightAsync(group, ParseInt(weight), null, ct)
                        .ConfigureAwait(false)
                );
                return true;
            case ["rename", .. var displayName] when displayName.Length > 0:
                Report(
                    await directory
                        .SetDisplayNameAsync(group, string.Join(' ', displayName), null, ct)
                        .ConfigureAwait(false)
                );
                return true;
            case ["set", var node, .. var rest]:
            {
                var (value, expiresAt) = ParseValueAndExpiry(rest);
                Report(
                    await directory
                        .SetNodeAsync(group, node, value, expiresAt, _mode, null, ct)
                        .ConfigureAwait(false)
                );
                return true;
            }
            case ["unset" or "unsettemp", var node]:
                Report(
                    await directory
                        .UnsetNodeAsync(group, node, args[0] == "unsettemp", null, ct)
                        .ConfigureAwait(false)
                );
                return true;
            case ["meta", "set", var key, var value, .. var rest]:
                Report(
                    await directory
                        .SetMetaAsync(group, key, value, ParseExpiry(rest), _mode, null, ct)
                        .ConfigureAwait(false)
                );
                return true;
            case ["meta", "unset" or "unsettemp", var key]:
                Report(
                    await directory
                        .UnsetMetaAsync(group, key, args[1] == "unsettemp", null, ct)
                        .ConfigureAwait(false)
                );
                return true;
            case ["parent", "add", var parent]:
                Report(
                    await directory.AddParentAsync(group, parent, null, ct).ConfigureAwait(false)
                );
                return true;
            case ["parent", "remove", var parent]:
                Report(
                    await directory.RemoveParentAsync(group, parent, null, ct).ConfigureAwait(false)
                );
                return true;
            default:
                return false;
        }
    }

    private async Task PrintUserAsync(IPlayerPermissionGrain grain, CancellationToken ct)
    {
        var assignments = await grain.GetAssignmentsAsync(ct).ConfigureAwait(false);
        var resolved = await grain.GetResolvedAsync(ct).ConfigureAwait(false);
        var client = await grain.GetClientStateAsync(ct).ConfigureAwait(false);
        var groups = (await Directory.GetSnapshotAsync(ct).ConfigureAwait(false)).Groups;

        System.Console.WriteLine("groups:");

        foreach (var membership in assignments.Groups)
            System.Console.WriteLine(
                $"  {(groups.TryGetValue(membership.GroupId, out var g) ? g.Name : $"#{membership.GroupId}")}{FormatExpiry(membership.ExpiresAt)}"
            );

        System.Console.WriteLine("  default (implicit)");

        System.Console.WriteLine("nodes:");

        foreach (var node in assignments.Nodes)
            System.Console.WriteLine(
                $"  {node.Node} = {Format(node.Value)}{FormatExpiry(node.ExpiresAt)}"
            );

        System.Console.WriteLine("meta:");

        foreach (var meta in assignments.Meta)
            System.Console.WriteLine($"  {meta.Key} = {meta.Value}{FormatExpiry(meta.ExpiresAt)}");

        System.Console.WriteLine($"holds ({resolved.Granted.Count}):");

        foreach (var node in resolved.Granted.Order(StringComparer.Ordinal))
            System.Console.WriteLine($"  {node}");

        foreach (var (key, value) in resolved.Meta)
            System.Console.WriteLine($"  meta {key} = {value}");

        System.Console.WriteLine(
            $"client: security level {client.SecurityLevel} ({(int)client.SecurityLevel}), "
                + $"ambassador {Format(client.IsAmbassador)}, moderator {Format(client.IsModerator)}"
        );
        System.Console.WriteLine(
            $"  perks allowed: {OrDash(string.Join(", ", client.Perks.Where(x => x.IsAllowed).Select(x => x.Perk)))}"
        );

        if (resolved.UnregisteredNodes.Length > 0)
            System.Console.WriteLine(
                $"unregistered: {string.Join(", ", resolved.UnregisteredNodes)}"
            );
    }

    private async Task PrintGroupAsync(string name, CancellationToken ct)
    {
        var groups = (await Directory.GetSnapshotAsync(ct).ConfigureAwait(false)).Groups;
        var group = groups.Values.FirstOrDefault(x => x.Name == name);

        if (group is null)
        {
            Report(PermissionChangeResultType.UnknownGroup);
            return;
        }

        System.Console.WriteLine(
            $"{group.Name} \"{group.DisplayName}\" weight {group.Weight} (id {group.Id})"
        );
        System.Console.WriteLine(
            $"parents: {OrDash(string.Join(", ", group.ParentIds.Select(id => groups.TryGetValue(id, out var p) ? p.Name : $"#{id}")))}"
        );

        foreach (var node in group.Nodes.OrderBy(x => x.Node, StringComparer.Ordinal))
            System.Console.WriteLine(
                $"  {node.Node} = {Format(node.Value)}{FormatExpiry(node.ExpiresAt)}"
            );

        foreach (var meta in group.Meta)
            System.Console.WriteLine(
                $"  meta {meta.Key} = {meta.Value}{FormatExpiry(meta.ExpiresAt)}"
            );
    }

    private static void PrintAudit(ImmutableArray<PermissionAuditSnapshot> rows)
    {
        if (rows.IsEmpty)
            System.Console.WriteLine("no audit rows");

        foreach (var row in rows)
            System.Console.WriteLine(
                $"{row.CreatedAt:yyyy-MM-dd HH:mm:ss}  {row.Action, -16} {row.Subject}"
                    + (row.Value is null ? "" : $" = {row.Value}")
                    + FormatExpiry(row.ExpiresAt)
                    + $"  by {(row.ActorPlayerId is { } actor ? $"player {actor}" : "console/system")}"
            );
    }

    private async Task<PlayerId?> FindPlayerAsync(string name, CancellationToken ct)
    {
        var playerId = await _grainFactory
            .GetPlayerDirectoryGrain()
            .GetPlayerIdAsync(name, ct)
            .ConfigureAwait(false);

        if (playerId is null)
            System.Console.WriteLine($"No player called '{name}'.");

        return playerId;
    }

    private static void Report(PermissionChangeResultType result) =>
        System.Console.WriteLine(
            result switch
            {
                PermissionChangeResultType.Changed => "Done.",
                PermissionChangeResultType.Unchanged => "Already so; nothing changed.",
                PermissionChangeResultType.UnknownGroup => "No such group.",
                PermissionChangeResultType.Invalid =>
                    "Malformed: nodes and keys are lowercase dotted segments (a-z, 0-9, _), groups one segment.",
                PermissionChangeResultType.Expired => "That expiry has already passed.",
                PermissionChangeResultType.ProtectedGroup =>
                    "The default group cannot be deleted, joined or left.",
                PermissionChangeResultType.WouldCycle =>
                    "That parent would make the group inherit from itself.",
                PermissionChangeResultType.AlreadyExists =>
                    "A group with that name already exists.",
                PermissionChangeResultType.NotFound => "Nothing of that name was set.",
                _ => result.ToString(),
            }
        );

    private static string Describe(PermissionAssignmentSourceSnapshot source) =>
        (
            source.SourceType == PermissionSourceType.Player
                ? "player"
                : $"group {source.GroupName} (weight {source.GroupWeight})"
        ) + $" : {source.Node} = {Format(source.Value)}{FormatExpiry(source.ExpiresAt)}";

    private static string OrDash(string value) => value.Length == 0 ? "-" : value;

    private static string Format(bool value) => value ? "true" : "false";

    private static string FormatExpiry(DateTime? expiresAt) =>
        expiresAt is { } at ? $"  (until {at:yyyy-MM-dd HH:mm} UTC)" : "";

    private static int ParseInt(string value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result
            : throw new FormatException($"'{value}' is not a whole number.");

    /// <summary>
    /// The optional value then optional duration after a node: <c>set room.* false 7d</c>,
    /// <c>set room.* 7d</c>, <c>set room.*</c>. The value defaults to true.
    /// </summary>
    private static (bool Value, DateTime? ExpiresAt) ParseValueAndExpiry(string[] rest) =>
        rest switch
        {
            [] => (true, null),
            ["true" or "false"] => (rest[0] == "true", null),
            ["true" or "false", var duration] => (rest[0] == "true", ParseExpiry([duration])),
            [var duration] => (true, ParseExpiry([duration])),
            _ => throw new FormatException("Expected [true|false] [duration]."),
        };

    private static DateTime? ParseExpiry(string[] rest)
    {
        if (rest.Length == 0)
            return null;

        if (rest.Length > 1)
            throw new FormatException("Expected at most one duration.");

        var text = rest[0];

        if (
            text.Length < 2
            || !int.TryParse(
                text.AsSpan(0, text.Length - 1),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var amount
            )
            || amount <= 0
        )
            throw new FormatException($"'{text}' is not a duration like 30m, 12h or 7d.");

        TimeSpan span = text[^1] switch
        {
            's' => TimeSpan.FromSeconds(amount),
            'm' => TimeSpan.FromMinutes(amount),
            'h' => TimeSpan.FromHours(amount),
            'd' => TimeSpan.FromDays(amount),
            'w' => TimeSpan.FromDays(amount * 7),
            _ => throw new FormatException($"'{text}' is not a duration like 30m, 12h or 7d."),
        };

        return DateTime.UtcNow + span;
    }
}
