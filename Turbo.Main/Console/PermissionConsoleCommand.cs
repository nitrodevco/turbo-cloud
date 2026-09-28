using System;
using System.Collections.Generic;
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
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Providers;
using Turbo.Primitives.Players.Snapshots.Permissions;

namespace Turbo.Main.Console;

/// <summary>
/// The <c>perm</c> console command: reads and edits groups and players' permissions through the
/// permission grains, as the console (audited with no actor). See <c>docs/permissions.md</c>.
/// </summary>
internal sealed class PermissionConsoleCommand(
    IGrainFactory grainFactory,
    IPermissionRegistryProvider permissionRegistryProvider
)
{
    public const string USAGE = """
            perm check <player> <node>                         why a player does or does not hold a node
            perm reload                                        re-read every group and online player from the database
            perm search <node> [count]                         groups and players given a node, exactly or by wildcard
            perm log [count]                                   recent changes to anyone
            perm log search <text> [count]                     recent changes whose node, key or group contains text
            perm user <player> info                            groups, nodes, meta and resolved set
            perm user <player> audit [count]
            perm user <player> reload                          re-read one player's rows from the database
            perm user <player> verbose on [prefix] | off       log every check made about the player
            perm user <player> group add <group> [duration] [--extend]
            perm user <player> group remove|removetemp <group>
            perm user <player> set <node> [true|false] [duration] [--extend]
            perm user <player> unset|unsettemp <node>
            perm user <player> meta set <key> <value> [duration] [--extend]
            perm user <player> meta unset|unsettemp <key>
            perm groups
            perm group <group> info
            perm group <group> audit [count]
            perm group <group> members [count]                 players in the group directly
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

    private const int DEFAULT_LOOKUP_COUNT = 50;

    private const string EXTEND_FLAG = "--extend";

    private readonly IGrainFactory _grainFactory = grainFactory;
    private readonly IPermissionRegistryProvider _permissionRegistryProvider =
        permissionRegistryProvider;

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
                ["reload"] => await ReloadAsync(ct).ConfigureAwait(false),
                ["search", var node] => await SearchAsync(node, DEFAULT_LOOKUP_COUNT, ct)
                    .ConfigureAwait(false),
                ["search", var node, var count] => await SearchAsync(node, ParseInt(count), ct)
                    .ConfigureAwait(false),
                ["log"] => await LogAsync(null, DEFAULT_AUDIT_COUNT, ct).ConfigureAwait(false),
                ["log", "search", var text] => await LogAsync(text, DEFAULT_AUDIT_COUNT, ct)
                    .ConfigureAwait(false),
                ["log", "search", var text, var count] => await LogAsync(text, ParseInt(count), ct)
                    .ConfigureAwait(false),
                ["log", var count] => await LogAsync(null, ParseInt(count), ct)
                    .ConfigureAwait(false),
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

    private async Task<bool> ReloadAsync(CancellationToken ct)
    {
        var players = await Directory.ReloadAsync(ct).ConfigureAwait(false);

        System.Console.WriteLine(
            $"Groups reloaded; {players} online players are reading their own rows again."
        );

        return true;
    }

    private async Task<bool> SearchAsync(string node, int count, CancellationToken ct)
    {
        if (!PermissionNodeFormat.IsValidNode(node))
        {
            Report(PermissionChangeResultType.Invalid);
            return true;
        }

        var holders = await Directory.FindNodeHoldersAsync(node, count, ct).ConfigureAwait(false);

        if (holders.IsEmpty)
        {
            System.Console.WriteLine($"Nobody is given {node}, directly or by wildcard.");
            return true;
        }

        var names = await NameTargetsAsync(holders.Select(x => (x.TargetType, x.TargetId)), ct)
            .ConfigureAwait(false);

        foreach (var holder in holders)
            System.Console.WriteLine(
                $"{names[(holder.TargetType, holder.TargetId)], -24} {holder.Assignment.Node} = "
                    + $"{Format(holder.Assignment.Value)}{FormatExpiry(holder.Assignment.ExpiresAt)}"
            );

        if (holders.Length >= count)
            System.Console.WriteLine($"(first {holders.Length}; ask for more with a count)");

        return true;
    }

    private async Task<bool> LogAsync(string? search, int count, CancellationToken ct)
    {
        var rows = await Directory.GetRecentAuditAsync(search, count, ct).ConfigureAwait(false);
        var names = await NameTargetsAsync(rows.Select(x => (x.TargetType, x.TargetId)), ct)
            .ConfigureAwait(false);

        PrintAudit(rows, row => names[(row.TargetType, row.TargetId)]);

        return true;
    }

    private async Task PrintMembersAsync(string group, int count, CancellationToken ct)
    {
        if (group == PermissionGroupNames.DEFAULT)
        {
            System.Console.WriteLine("Every player holds default, without a row.");
            return;
        }

        var members = await Directory.GetMembersAsync(group, count, ct).ConfigureAwait(false);

        if (members.IsEmpty)
        {
            System.Console.WriteLine("No members (or no such group).");
            return;
        }

        var names = await _grainFactory
            .GetPlayerDirectoryGrain()
            .GetPlayerNamesAsync([.. members.Select(x => x.PlayerId)], ct)
            .ConfigureAwait(false);

        foreach (var member in members)
            System.Console.WriteLine(
                $"  {names.GetValueOrDefault(member.PlayerId, $"#{member.PlayerId.Value}")}"
                    + FormatExpiry(member.ExpiresAt)
            );

        if (members.Length >= count)
            System.Console.WriteLine($"(first {members.Length}; ask for more with a count)");
    }

    /// <summary>"group vip" or "player Alice" for each audit or search target, for printing.</summary>
    private async Task<
        ImmutableDictionary<(PermissionAuditTargetType, int), string>
    > NameTargetsAsync(
        IEnumerable<(PermissionAuditTargetType Type, int Id)> targets,
        CancellationToken ct
    )
    {
        var all = targets.Distinct().ToList();
        var groups = (await Directory.GetSnapshotAsync(ct).ConfigureAwait(false)).Groups;
        var players = await _grainFactory
            .GetPlayerDirectoryGrain()
            .GetPlayerNamesAsync(
                [
                    .. all.Where(x => x.Type == PermissionAuditTargetType.Player)
                        .Select(x => PlayerId.Parse(x.Id)),
                ],
                ct
            )
            .ConfigureAwait(false);

        return all.ToImmutableDictionary(
            x => (x.Type, x.Id),
            x =>
                x.Type == PermissionAuditTargetType.Group
                    ? $"group {(groups.TryGetValue(x.Id, out var group) ? group.Name : $"#{x.Id}")}"
                    : $"player {players.GetValueOrDefault(PlayerId.Parse(x.Id), $"#{x.Id}")}"
        );
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
            case ["reload"]:
                await grain.ReloadAsync(ct).ConfigureAwait(false);
                System.Console.WriteLine("Reloaded.");
                return true;
            case ["verbose", "on", .. var filter] when filter.Length <= 1:
                await grain
                    .SetVerboseAsync(filter.Length == 0 ? "" : filter[0], ct)
                    .ConfigureAwait(false);
                System.Console.WriteLine(
                    "Checks made about the player are logged now, until verbose off or their grain goes idle."
                );
                return true;
            case ["verbose", "off"]:
                await grain.SetVerboseAsync(null, ct).ConfigureAwait(false);
                System.Console.WriteLine("Verbose off.");
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
            case ["members"]:
                await PrintMembersAsync(group, DEFAULT_LOOKUP_COUNT, ct).ConfigureAwait(false);
                return true;
            case ["members", var count]:
                await PrintMembersAsync(group, ParseInt(count), ct).ConfigureAwait(false);
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

        PrintLevel(PermissionProjection.ReportLevel(_permissionRegistryProvider.Current, resolved));

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

        PrintGroupLevel(group, groups);
    }

    /// <summary>
    /// What a player holding this group (and default) would be sent, worked out here against the
    /// directory's groups: the level is a property of the whole inheritance, not of one group's
    /// own nodes.
    /// </summary>
    private void PrintGroupLevel(
        PermissionGroupSnapshot group,
        ImmutableDictionary<int, PermissionGroupSnapshot> groups
    )
    {
        var registry = _permissionRegistryProvider.Current;
        var member = new PlayerPermissionAssignmentsSnapshot
        {
            Groups = [new PermissionGroupMembershipSnapshot { GroupId = group.Id }],
            Nodes = [],
            Meta = [],
        };

        PrintLevel(
            PermissionProjection.ReportLevel(
                registry,
                PermissionResolver.Resolve(registry, groups, member, DateTime.UtcNow)
            )
        );
    }

    private static void PrintLevel(PermissionLevelReport report)
    {
        if (report.Source is null)
        {
            System.Console.WriteLine("client level: None (0)");
            return;
        }

        System.Console.WriteLine(
            $"client level: {report.Level} ({(int)report.Level}), set by {report.Source}"
        );

        if (report.ShownButRefused.IsEmpty)
            return;

        // The client reads the level as a threshold: it draws these too, and the server refuses them.
        System.Console.WriteLine("  the client will also offer, and the server refuse:");

        foreach (var node in report.ShownButRefused)
            System.Console.WriteLine(
                $"    {node.Node, -42} ({(int)node.ClientLevel!.Value})  {node.Description}"
            );
    }

    /// <summary>Prints audit rows; <paramref name="target"/> names whom each is about, for a log of everyone.</summary>
    private static void PrintAudit(
        ImmutableArray<PermissionAuditSnapshot> rows,
        Func<PermissionAuditSnapshot, string>? target = null
    )
    {
        if (rows.IsEmpty)
            System.Console.WriteLine("no audit rows");

        foreach (var row in rows)
            System.Console.WriteLine(
                $"{row.CreatedAt:yyyy-MM-dd HH:mm:ss}  "
                    + (target is null ? "" : $"{target(row), -24} ")
                    + $"{row.Action, -16} {row.Subject}"
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
                PermissionChangeResultType.ReservedNode =>
                    "group.<name> follows from holding the group; add the player to it instead.",
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
