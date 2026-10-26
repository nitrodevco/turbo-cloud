using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Orleans;
using Turbo.Admin.Commands;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Plugins;

namespace Turbo.Admin.Api;

/// <summary>
/// Who gets which theme (<c>docs/plugin-data.md</c>, "Who gets which theme"), for staff with
/// <c>admin.plugins.view</c>: each theme's status, whether it is the default or may be chosen, and
/// its development audience (players by name, permission groups). A change needs
/// <c>admin.plugins.manage</c> and goes to the command log.
/// </summary>
internal sealed class ThemeAudienceEndpoints(
    IGrainFactory grainFactory,
    AdminActionLog audit,
    IThemeAudiences? themes = null
)
{
    private const string NO_ACCESS = "You can't see the server's plugins.";
    private const string NO_MANAGE = "You can't change the server's plugins.";

    public void Map(RouteGroupBuilder secured)
    {
        var group = secured
            .MapGroup("/plugins/themes")
            .RequirePermission(PermissionNodes.Admin.PLUGINS_VIEW, NO_ACCESS)
            .ChecksInHandler(
                "A change checks admin.plugins.manage in the handler, so a refusal is written to the command log too."
            );

        group.MapGet("/audience", ListAsync);
        group.MapPut("/{key}/audience", SetAsync);
    }

    private async Task<IResult> ListAsync(HttpContext http, CancellationToken ct)
    {
        if (themes is null)
            return AdminResults.Error(StatusCodes.Status404NotFound, "The server runs no plugins.");

        var list = await themes.ListAsync(ct).ConfigureAwait(false);
        var names = await grainFactory
            .GetPlayerDirectoryGrain()
            .GetPlayerNamesAsync(
                [.. list.SelectMany(x => x.Players).Distinct().Select(x => new PlayerId(x))],
                ct
            )
            .ConfigureAwait(false);
        var groups = await grainFactory
            .GetPermissionGroupDirectoryGrain()
            .GetSnapshotAsync(ct)
            .ConfigureAwait(false);

        return Results.Ok(
            new ThemeAudiencesResponse(
                [
                    .. list.Select(x => new ThemeAudienceView(
                        x.Key,
                        x.Extends,
                        x.Status.ToString().ToLowerInvariant(),
                        x.IsDefault,
                        x.Selectable,
                        [
                            .. x.Players.Select(id => new ThemeAudiencePlayer(
                                id,
                                names.GetValueOrDefault(new PlayerId(id))
                            )),
                        ],
                        x.Groups
                    )),
                ],
                [.. groups.Groups.Values.OrderByDescending(x => x.Weight).Select(x => x.Name)],
                await AdminIdentity
                    .Of(http)
                    .HoldsAsync(grainFactory, PermissionNodes.Admin.PLUGINS_MANAGE, ct)
                    .ConfigureAwait(false)
            )
        );
    }

    private async Task<IResult> SetAsync(
        string key,
        ThemeAudienceInput input,
        HttpContext http,
        CancellationToken ct
    )
    {
        var player = AdminIdentity.Of(http).PlayerId;
        var arguments =
            $"theme {key} audience {input.Status ?? "-"} default={input.IsDefault?.ToString() ?? "-"} selectable={input.Selectable?.ToString() ?? "-"}";

        if (
            !await AdminIdentity
                .Of(http)
                .HoldsAsync(grainFactory, PermissionNodes.Admin.PLUGINS_MANAGE, ct)
                .ConfigureAwait(false)
        )
        {
            await audit
                .WriteAsync(AdminActionLog.PLUGIN, player, arguments, CommandOutcome.Refused, ct)
                .ConfigureAwait(false);

            return AdminResults.Error(StatusCodes.Status403Forbidden, NO_MANAGE);
        }

        if (themes is null)
            return AdminResults.Error(StatusCodes.Status404NotFound, "The server runs no plugins.");

        IResult result;
        var outcome = CommandOutcome.Completed;

        try
        {
            ThemeStatus? status =
                input.Status is null ? null
                : Enum.TryParse<ThemeStatus>(input.Status, ignoreCase: true, out var parsed)
                && Enum.IsDefined(parsed)
                    ? parsed
                : throw new ArgumentException(
                    "A theme's status is live, development or off.",
                    nameof(input)
                );

            var players = input.Players is null
                ? (ImmutableArray<int>?)null
                : await IdsAsync(input.Players, ct).ConfigureAwait(false);
            var saved = await themes
                .SetAsync(
                    key,
                    new ThemeAudienceChange(
                        status,
                        input.IsDefault,
                        input.Selectable,
                        players,
                        input.Groups?.ToImmutableArray()
                    ),
                    player,
                    ct
                )
                .ConfigureAwait(false);

            result = Results.Ok(
                new
                {
                    key = saved.Key,
                    status = saved.Status.ToString().ToLowerInvariant(),
                    isDefault = saved.IsDefault,
                    selectable = saved.Selectable,
                }
            );
        }
        catch (ArgumentException ex)
        {
            outcome = CommandOutcome.Failed;
            result = AdminResults.Error(StatusCodes.Status400BadRequest, ex.Message);
        }

        await audit
            .WriteAsync(AdminActionLog.PLUGIN, player, arguments, outcome, CancellationToken.None)
            .ConfigureAwait(false);

        return result;
    }

    /// <summary>The players named, by name or id; throws naming one that is nobody.</summary>
    private async Task<ImmutableArray<int>> IdsAsync(
        IEnumerable<string> players,
        CancellationToken ct
    )
    {
        var ids = ImmutableArray.CreateBuilder<int>();
        var directory = grainFactory.GetPlayerDirectoryGrain();

        foreach (var named in players.Select(x => x.Trim()).Where(x => x.Length > 0))
        {
            if (int.TryParse(named, out var id) && id > 0)
                ids.Add(id);
            else if (await directory.GetPlayerIdAsync(named, ct).ConfigureAwait(false) is { } found)
                ids.Add(found.Value);
            else
                throw new ArgumentException($"There is no player called {named}.", nameof(players));
        }

        return ids.ToImmutable();
    }
}

/// <summary>A change to who gets a theme; a field left out is left as it is. Players by name or id.</summary>
internal sealed record ThemeAudienceInput(
    string? Status,
    bool? IsDefault,
    bool? Selectable,
    string[]? Players,
    string[]? Groups
);

internal sealed record ThemeAudiencePlayer(int Id, string? Name);

internal sealed record ThemeAudienceView(
    string Key,
    string? Extends,
    string Status,
    bool IsDefault,
    bool Selectable,
    ImmutableArray<ThemeAudiencePlayer> Players,
    ImmutableArray<string> Groups
);

/// <summary>Every theme with who gets it, the permission groups an audience may name, and whether the caller may change them.</summary>
internal sealed record ThemeAudiencesResponse(
    ImmutableArray<ThemeAudienceView> Themes,
    ImmutableArray<string> Groups,
    bool CanManage
);
