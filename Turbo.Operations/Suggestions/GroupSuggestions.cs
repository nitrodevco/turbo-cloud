using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Orleans;

namespace Turbo.Operations.Suggestions;

/// <summary>Permission group names, as <c>:group</c> takes them.</summary>
public sealed class GroupSuggestions(IGrainFactory grainFactory, TimeProvider timeProvider)
    : ISuggestionSource
{
    public string Name => SuggestionSources.GROUPS;

    public async Task<IReadOnlyList<string>> SuggestAsync(
        CommandSuggestionContext context,
        string prefix,
        int limit,
        CancellationToken ct
    )
    {
        if (
            !context.Permissions.Has(
                Turbo.Primitives.Players.Permissions.PermissionNodes.Permissions.MANAGE
            )
        )
            return [];
        var groups = (
            await grainFactory.GetPermissionGroupDirectoryGrain().GetSnapshotAsync(ct)
        ).Groups.Values.AsEnumerable();
        if (
            context.Command == "group"
            && context.Syntax == "remove"
            && context.Arguments.TryGetValue("who", out var who)
        )
        {
            var playerId = await grainFactory.GetPlayerDirectoryGrain().GetPlayerIdAsync(who, ct);
            if (playerId is null)
                return [];
            var assignments = await grainFactory
                .GetPlayerPermissionGrain(playerId.Value)
                .GetAssignmentsAsync(ct);
            var now = timeProvider.GetUtcNow().UtcDateTime;
            var ids = assignments
                .Groups.Where(x => x.ExpiresAt is null || x.ExpiresAt > now)
                .Select(x => x.GroupId)
                .ToHashSet();
            groups = groups.Where(x => ids.Contains(x.Id));
        }
        return
        [
            .. groups
                .Select(x => x.Name)
                .Where(x => x.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Order(StringComparer.Ordinal)
                .Take(limit),
        ];
    }
}
