using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Commands.Enums;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;

namespace Turbo.Commands;

/// <summary>
/// Answers a client's request for the values of one parameter. It answers only for a command the
/// player may use, so a suggestion never tells a player more than the command would, and it has a
/// small per-player limit of its own, apart from chat flood.
/// </summary>
public sealed class CommandSuggestionService(
    ICommandRegistryProvider registryProvider,
    SuggestionSourceRegistry sources,
    IGrainFactory grainFactory,
    ISessionGateway sessionGateway,
    IOptions<CommandConfig> config,
    TimeProvider timeProvider,
    ILogger<ICommandSuggestionService> logger
) : ICommandSuggestionService, IDisposable
{
    private static readonly ImmutableArray<string> DURATIONS = ["30m", "1h", "1d", "7d", "perm"];

    private static readonly ImmutableArray<string> BOOLEANS = ["true", "false"];

    private static readonly ImmutableArray<string> SELECTORS =
    [
        PlayerTarget.ONLINE,
        PlayerTarget.ROOM,
    ];

    /// <summary>Each player's requests in the current second: the second, and how many.</summary>
    private readonly ConcurrentDictionary<int, (long Second, int Count)> _windows = new();
    private readonly Lock _windowGate = new();
    private ITimer? _cleanupTimer;

    public async Task<ImmutableArray<string>> SuggestAsync(
        PlayerId playerId,
        string command,
        int parameter,
        string prefix,
        CancellationToken ct,
        string syntax = "",
        string argumentText = ""
    )
    {
        if (!TryTake(playerId))
            return [];

        if (!registryProvider.Current.TryFind(command, out var descriptor) || parameter < 0)
            return [];

        var branch = descriptor.Binder.Syntax.FirstOrDefault(x =>
            x.Path.Equals(syntax, StringComparison.OrdinalIgnoreCase)
        );
        if (
            (descriptor.Binder.Syntax.Count > 0 && branch is null)
            || (descriptor.Binder.Syntax.Count == 0 && syntax.Length > 0)
        )
            return [];
        var binder = branch?.Binder ?? descriptor.Binder;
        if (parameter >= binder.Parameters.Count)
            return [];
        var info = binder.Parameters[parameter];

        if (info.Suggest == CommandSuggestType.None)
            return [];

        var permissions = await grainFactory
            .GetPlayerPermissionGrain(playerId)
            .GetResolvedAsync(ct);

        if (
            !CommandTreeBuilder.MayUse(descriptor, permissions)
            || (branch?.Permission is { } branchNode && !permissions.Has(branchNode))
        )
            return [];

        var limit = config.Value.MaxSuggestions;

        return info.Kind switch
        {
            CommandParameterKind.Enumeration => Filter(info.Members, prefix, limit),
            CommandParameterKind.Boolean => Filter(BOOLEANS, prefix, limit),
            CommandParameterKind.Duration => Filter(DURATIONS, prefix, limit),
            CommandParameterKind.Player => await PlayersAsync(
                prefix,
                info.SelectorNode is { } node && permissions.Has(node),
                limit,
                ct
            ),
            CommandParameterKind.Word when info.SuggestionSource is { } name =>
                await FromSourceAsync(
                    name,
                    prefix,
                    limit,
                    playerId,
                    permissions,
                    descriptor.Name,
                    syntax,
                    binder,
                    parameter,
                    argumentText,
                    ct
                ),
            // A room's own users are on the client already.
            _ => [],
        };
    }

    private async Task<ImmutableArray<string>> PlayersAsync(
        string prefix,
        bool selectors,
        int limit,
        CancellationToken ct
    )
    {
        if (prefix.StartsWith(PlayerTarget.SELECTOR_PREFIX))
            return selectors ? Filter(SELECTORS, prefix, limit) : [];

        if (prefix.Length < config.Value.MinPlayerPrefix)
            return [];

        var names = await grainFactory
            .GetPlayerDirectoryGrain()
            .SearchNamesAsync(prefix, limit, ct);

        if (names.IsEmpty)
            return names;

        // Those online first, as they are the likelier to be meant; the directory sorts the rest.
        var online = (
            await grainFactory
                .GetPlayerDirectoryGrain()
                .GetPlayerNamesAsync([.. sessionGateway.GetOnlinePlayerIds()], ct)
        ).Values.ToHashSet(StringComparer.OrdinalIgnoreCase);

        return [.. names.OrderBy(name => online.Contains(name) ? 0 : 1)];
    }

    private async Task<ImmutableArray<string>> FromSourceAsync(
        string name,
        string prefix,
        int limit,
        PlayerId playerId,
        Turbo.Primitives.Players.Snapshots.Permissions.ResolvedPermissionsSnapshot permissions,
        string command,
        string syntax,
        ICommandBinder binder,
        int parameter,
        string argumentText,
        CancellationToken ct
    )
    {
        if (!sources.TryFind(name, out var source))
        {
            logger.LogWarning("No suggestion source is registered as {Source}", name);

            return [];
        }

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var cursor = 0;
        for (var index = 0; index < parameter; index++)
        {
            var token = CommandTextReader.Read(argumentText, ref cursor);
            if (!token.Valid || token.Start == token.End)
                break;
            values[binder.Parameters[index].Name] = token.Text;
        }
        try
        {
            var room = await grainFactory.GetPlayerPresenceGrain(playerId).GetActiveRoomAsync(ct);
            var context = new CommandSuggestionContext(
                playerId,
                room is not null && room.RoomId.Value > 0 ? room.RoomId : null,
                permissions,
                command,
                syntax,
                new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(values)
            );
            return
            [
                .. (await source.SuggestAsync(context, prefix, limit, ct))
                    .Where(x => x.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(limit),
            ];
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Suggestion source {Source} failed", name);
            return [];
        }
    }

    private static ImmutableArray<string> Filter(
        IEnumerable<string> values,
        string prefix,
        int limit
    ) =>
        [
            .. values
                .Where(x => x.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Take(limit),
        ];

    /// <summary>Counts one request against the player's second; false when it is over.</summary>
    private bool TryTake(PlayerId playerId)
    {
        var second = timeProvider.GetUtcNow().ToUnixTimeSeconds();
        var allowed = config.Value.SuggestionsPerSecond;
        lock (_windowGate)
        {
            _cleanupTimer ??= timeProvider.CreateTimer(
                _ => Sweep(),
                null,
                TimeSpan.FromSeconds(config.Value.SuggestionWindowRetentionSeconds),
                TimeSpan.FromSeconds(config.Value.SuggestionWindowRetentionSeconds)
            );
            if (
                !_windows.ContainsKey(playerId.Value)
                && _windows.Count >= config.Value.MaxSuggestionWindows
            )
            {
                Sweep();
                if (_windows.Count >= config.Value.MaxSuggestionWindows)
                    return false;
            }
            var window = _windows.AddOrUpdate(
                playerId.Value,
                _ => (second, 1),
                (_, current) => current.Second == second ? (second, current.Count + 1) : (second, 1)
            );

            return window.Count <= allowed;
        }
    }

    private void Sweep()
    {
        lock (_windowGate)
        {
            var before =
                timeProvider.GetUtcNow().ToUnixTimeSeconds()
                - config.Value.SuggestionWindowRetentionSeconds;
            foreach (var pair in _windows)
                if (pair.Value.Second <= before)
                    _windows.TryRemove(pair.Key, out _);
        }
    }

    public void Dispose()
    {
        lock (_windowGate)
            _cleanupTimer?.Dispose();
    }
}
