using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Orleans;
using Turbo.Admin.Api.Contracts;
using Turbo.Admin.Permissions;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Grains.Permissions;

namespace Turbo.Admin.Api;

/// <summary>
/// One player's permissions: what is set on them, what it resolves to and why, and changing it
/// (<c>permissions.manage</c>, within <see cref="PermissionEditor"/>'s rule: only a player lighter
/// than the editor). Changes go through the player's permission grain as the signed-in player,
/// which audits them and tells the client and room at once.
/// </summary>
internal sealed class PermissionPlayerEndpoints(
    IGrainFactory grainFactory,
    PermissionEditPolicy policy,
    PermissionViews views,
    TimeProvider timeProvider
)
{
    private const int DEFAULT_COUNT = 50;

    private DateTime UtcNow => timeProvider.GetUtcNow().UtcDateTime;

    public void Map(RouteGroupBuilder secured)
    {
        var group = secured
            .MapGroup("/permissions/players")
            .AddEndpointFilter(PermissionResults.RequireView(grainFactory));

        group.MapGet("/", ListAsync);
        group.MapGet("/find", FindAsync);
        group.MapGet("/{id:int}", GetAsync);
        group.MapGet("/{id:int}/check", ExplainAsync);
        group.MapGet("/{id:int}/audit", AuditAsync);
        group.MapPost("/{id:int}/groups", AddGroupAsync);
        group.MapDelete("/{id:int}/groups/{groupName}", RemoveGroupAsync);
        group.MapPut("/{id:int}/nodes", SetNodeAsync);
        group.MapDelete("/{id:int}/nodes", UnsetNodeAsync);
        group.MapPut("/{id:int}/meta", SetMetaAsync);
        group.MapDelete("/{id:int}/meta", UnsetMetaAsync);
    }

    /// <summary>Everyone in a group besides default: the staff list.</summary>
    private async Task<IResult> ListAsync(HttpContext http, int? count, CancellationToken ct) =>
        Results.Ok(
            await views
                .GetStaffAsync(
                    await EditorAsync(http, ct).ConfigureAwait(false),
                    count ?? DEFAULT_COUNT,
                    ct
                )
                .ConfigureAwait(false)
        );

    private async Task<IResult> FindAsync(string? name, CancellationToken ct)
    {
        var trimmed = name?.Trim() ?? string.Empty;

        return
            trimmed.Length > 0
            && await grainFactory
                .GetPlayerDirectoryGrain()
                .GetPlayerIdAsync(trimmed, ct)
                .ConfigureAwait(false)
                is { } id
            ? Results.Ok(new PermissionMemberView(id.Value, trimmed, null))
            : AdminResults.Error(StatusCodes.Status404NotFound, $"No player is called {trimmed}.");
    }

    private async Task<IResult> GetAsync(HttpContext http, int id, CancellationToken ct)
    {
        if (await NameOfAsync(id, ct).ConfigureAwait(false) is not { } name)
            return NoSuchPlayer(id);

        return Results.Ok(
            await views
                .GetPlayerAsync(await EditorAsync(http, ct).ConfigureAwait(false), id, name, ct)
                .ConfigureAwait(false)
        );
    }

    private async Task<IResult> ExplainAsync(int id, string? node, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(node))
            return AdminResults.Error(StatusCodes.Status400BadRequest, "Name a node to check.");

        return Results.Ok(
            PermissionViews.CheckOf(
                await Grain(id).ExplainAsync(node.Trim(), ct).ConfigureAwait(false)
            )
        );
    }

    private async Task<IResult> AuditAsync(int id, int? count, CancellationToken ct) =>
        Results.Ok(
            await views
                .NameAuditAsync(
                    await Grain(id).GetAuditAsync(count ?? DEFAULT_COUNT, ct).ConfigureAwait(false),
                    ct
                )
                .ConfigureAwait(false)
        );

    private async Task<IResult> AddGroupAsync(
        HttpContext http,
        int id,
        AddPermissionGroupMemberRequest request,
        CancellationToken ct
    )
    {
        var group = request.Group?.Trim().ToLowerInvariant() ?? string.Empty;

        if (await CheckAsync(http, id, group, null, ct).ConfigureAwait(false) is { } refused)
            return refused;

        if (!PermissionResults.TryExpiry(request.Duration, UtcNow, out var expiresAt))
            return PermissionResults.BadDuration(request.Duration);

        return PermissionResults.Changed(
            await Grain(id)
                .AddGroupAsync(
                    group,
                    expiresAt,
                    PermissionResults.ModeOf(request.Extend),
                    Actor(http),
                    ct
                )
                .ConfigureAwait(false)
        );
    }

    private async Task<IResult> RemoveGroupAsync(
        HttpContext http,
        int id,
        string groupName,
        bool? temporary,
        CancellationToken ct
    )
    {
        if (await CheckAsync(http, id, groupName, null, ct).ConfigureAwait(false) is { } refused)
            return refused;

        return PermissionResults.Changed(
            await Grain(id)
                .RemoveGroupAsync(groupName, temporary ?? false, Actor(http), ct)
                .ConfigureAwait(false)
        );
    }

    private async Task<IResult> SetNodeAsync(
        HttpContext http,
        int id,
        SetPermissionNodeRequest request,
        CancellationToken ct
    )
    {
        var node = request.Node?.Trim() ?? string.Empty;

        if (await CheckAsync(http, id, null, node, ct).ConfigureAwait(false) is { } refused)
            return refused;

        if (!PermissionResults.TryExpiry(request.Duration, UtcNow, out var expiresAt))
            return PermissionResults.BadDuration(request.Duration);

        return PermissionResults.Changed(
            await Grain(id)
                .SetNodeAsync(
                    node,
                    request.Value,
                    expiresAt,
                    PermissionResults.ModeOf(request.Extend),
                    Actor(http),
                    ct
                )
                .ConfigureAwait(false)
        );
    }

    private async Task<IResult> UnsetNodeAsync(
        HttpContext http,
        int id,
        string? node,
        bool? temporary,
        CancellationToken ct
    )
    {
        node = node?.Trim() ?? string.Empty;

        if (await CheckAsync(http, id, null, node, ct).ConfigureAwait(false) is { } refused)
            return refused;

        return PermissionResults.Changed(
            await Grain(id)
                .UnsetNodeAsync(node, temporary ?? false, Actor(http), ct)
                .ConfigureAwait(false)
        );
    }

    private async Task<IResult> SetMetaAsync(
        HttpContext http,
        int id,
        SetPermissionMetaRequest request,
        CancellationToken ct
    )
    {
        if (await CheckAsync(http, id, null, null, ct).ConfigureAwait(false) is { } refused)
            return refused;

        if (!PermissionResults.TryExpiry(request.Duration, UtcNow, out var expiresAt))
            return PermissionResults.BadDuration(request.Duration);

        return PermissionResults.Changed(
            await Grain(id)
                .SetMetaAsync(
                    request.Key?.Trim() ?? string.Empty,
                    request.Value?.Trim() ?? string.Empty,
                    expiresAt,
                    PermissionResults.ModeOf(request.Extend),
                    Actor(http),
                    ct
                )
                .ConfigureAwait(false)
        );
    }

    private async Task<IResult> UnsetMetaAsync(
        HttpContext http,
        int id,
        string? key,
        bool? temporary,
        CancellationToken ct
    )
    {
        if (await CheckAsync(http, id, null, null, ct).ConfigureAwait(false) is { } refused)
            return refused;

        return PermissionResults.Changed(
            await Grain(id)
                .UnsetMetaAsync(key?.Trim() ?? string.Empty, temporary ?? false, Actor(http), ct)
                .ConfigureAwait(false)
        );
    }

    /// <summary>
    /// The player exists and is the editor's to change; and, when given, the group is theirs to
    /// hand out or take away and the node theirs to grant or deny. Null when all of it holds.
    /// </summary>
    private async Task<IResult?> CheckAsync(
        HttpContext http,
        int id,
        string? group,
        string? node,
        CancellationToken ct
    )
    {
        if (await NameOfAsync(id, ct).ConfigureAwait(false) is not { } name)
            return NoSuchPlayer(id);

        // The rule refuses it too (nobody is lighter than themselves); this says it plainly.
        if (AdminIdentity.Of(http).PlayerId.Value == id)
            return AdminResults.Error(
                StatusCodes.Status403Forbidden,
                "You can't change your own permissions."
            );

        var editor = await EditorAsync(http, ct).ConfigureAwait(false);

        if (
            await editor.CheckPlayerAsync(id, ct).ConfigureAwait(false)
            is not PermissionEditRefusal.None
                and var refusal
        )
            return PermissionResults.Refused(refusal, editor, name);

        if (
            group is not null
            && editor.CheckGroup(group) is not PermissionEditRefusal.None and var heavy
        )
            return PermissionResults.Refused(heavy, editor, group);

        if (
            node is not null
            && editor.CheckAssignment(node) is not PermissionEditRefusal.None and var notHeld
        )
            return PermissionResults.Refused(notHeld, editor, node);

        return null;
    }

    /// <summary>The player's name, or null when nobody has the id.</summary>
    private async Task<string?> NameOfAsync(int id, CancellationToken ct)
    {
        var names = await grainFactory
            .GetPlayerDirectoryGrain()
            .GetPlayerNamesAsync([PlayerId.Parse(id)], ct)
            .ConfigureAwait(false);

        return names.TryGetValue(PlayerId.Parse(id), out var name) ? name : null;
    }

    private static IResult NoSuchPlayer(int id) =>
        AdminResults.Error(StatusCodes.Status404NotFound, $"There is no player {id}.");

    private IPlayerPermissionGrain Grain(int id) =>
        grainFactory.GetPlayerPermissionGrain(PlayerId.Parse(id));

    private Task<PermissionEditor> EditorAsync(HttpContext http, CancellationToken ct) =>
        policy.ForAsync(AdminIdentity.Of(http).PlayerId, ct);

    private static PlayerId Actor(HttpContext http) => AdminIdentity.Of(http).PlayerId;
}
