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
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Grains.Permissions;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Admin.Api;

/// <summary>
/// Permission groups: seeing them (<c>admin.permissions.view</c>) and changing them
/// (<c>permissions.manage</c>, within <see cref="PermissionEditor"/>'s rule). Every change goes
/// through the group directory grain as the signed-in player, so it is audited as theirs.
/// Nodes and meta keys travel in the query or body, not the path: they hold dots and <c>*</c>.
/// </summary>
internal sealed class PermissionGroupEndpoints(
    IGrainFactory grainFactory,
    IPermissionEditService permissions,
    PermissionViews views,
    TimeProvider timeProvider
)
{
    private const int DEFAULT_COUNT = 50;

    private IPermissionGroupDirectoryGrain Directory =>
        grainFactory.GetPermissionGroupDirectoryGrain();

    private DateTime UtcNow => timeProvider.GetUtcNow().UtcDateTime;

    public void Map(RouteGroupBuilder secured)
    {
        var group = secured
            .MapGroup("/permissions/groups")
            .AddEndpointFilter(PermissionResults.RequireView(grainFactory));

        group.MapGet("/", ListAsync);
        group.MapPost("/", CreateAsync);
        group.MapGet("/{name}", GetAsync);
        group.MapPut("/{name}", UpdateAsync);
        group.MapDelete("/{name}", DeleteAsync);
        group.MapGet("/{name}/members", MembersAsync);
        group.MapGet("/{name}/audit", AuditAsync);
        group.MapPut("/{name}/nodes", SetNodeAsync);
        group.MapDelete("/{name}/nodes", UnsetNodeAsync);
        group.MapPut("/{name}/meta", SetMetaAsync);
        group.MapDelete("/{name}/meta", UnsetMetaAsync);
        group.MapPost("/{name}/parents", AddParentAsync);
        group.MapDelete("/{name}/parents/{parent}", RemoveParentAsync);
    }

    private async Task<IResult> ListAsync(HttpContext http, CancellationToken ct) =>
        Results.Ok(views.ListGroups(await EditorAsync(http, ct).ConfigureAwait(false)));

    private async Task<IResult> GetAsync(HttpContext http, string name, CancellationToken ct) =>
        views.GetGroup(await EditorAsync(http, ct).ConfigureAwait(false), name) is { } group
            ? Results.Ok(group)
            : AdminResults.Error(StatusCodes.Status404NotFound, $"There is no group {name}.");

    private async Task<IResult> MembersAsync(string name, int? count, CancellationToken ct) =>
        Results.Ok(
            await views.GetMembersAsync(name, count ?? DEFAULT_COUNT, ct).ConfigureAwait(false)
        );

    private async Task<IResult> AuditAsync(string name, int? count, CancellationToken ct) =>
        Results.Ok(
            await views
                .NameAuditAsync(
                    await Directory
                        .GetAuditAsync(name, count ?? DEFAULT_COUNT, ct)
                        .ConfigureAwait(false),
                    ct
                )
                .ConfigureAwait(false)
        );

    private async Task<IResult> CreateAsync(
        HttpContext http,
        CreatePermissionGroupRequest request,
        CancellationToken ct
    )
    {
        var editor = await EditorAsync(http, ct).ConfigureAwait(false);
        var name = request.Name?.Trim().ToLowerInvariant() ?? string.Empty;

        if (editor.CheckWeight(request.Weight) is not PermissionEditRefusal.None and var refusal)
            return PermissionResults.Refused(refusal, editor, name);

        var displayName = string.IsNullOrWhiteSpace(request.DisplayName)
            ? name
            : request.DisplayName.Trim();

        return PermissionResults.Changed(
            await Directory
                .CreateGroupAsync(name, displayName, request.Weight, Actor(http), ct)
                .ConfigureAwait(false)
        );
    }

    private async Task<IResult> UpdateAsync(
        HttpContext http,
        string name,
        UpdatePermissionGroupRequest request,
        CancellationToken ct
    )
    {
        var editor = await EditorAsync(http, ct).ConfigureAwait(false);

        if (editor.CheckGroup(name) is not PermissionEditRefusal.None and var refusal)
            return PermissionResults.Refused(refusal, editor, name);

        if (
            request.Weight is { } weight
            && editor.CheckWeight(weight) is not PermissionEditRefusal.None and var heavy
        )
            return PermissionResults.Refused(heavy, editor, name);

        var changed = false;

        if (!string.IsNullOrWhiteSpace(request.DisplayName))
        {
            var renamed = await Directory
                .SetDisplayNameAsync(name, request.DisplayName.Trim(), Actor(http), ct)
                .ConfigureAwait(false);

            if (
                renamed
                is not (PermissionChangeResultType.Changed or PermissionChangeResultType.Unchanged)
            )
                return PermissionResults.Changed(renamed);

            changed |= renamed == PermissionChangeResultType.Changed;
        }

        if (request.Weight is { } newWeight)
        {
            var reweighted = await Directory
                .SetWeightAsync(name, newWeight, Actor(http), ct)
                .ConfigureAwait(false);

            if (
                reweighted
                is not (PermissionChangeResultType.Changed or PermissionChangeResultType.Unchanged)
            )
                return PermissionResults.Changed(reweighted);

            changed |= reweighted == PermissionChangeResultType.Changed;
        }

        return PermissionResults.Changed(
            changed ? PermissionChangeResultType.Changed : PermissionChangeResultType.Unchanged
        );
    }

    private async Task<IResult> DeleteAsync(HttpContext http, string name, CancellationToken ct)
    {
        var editor = await EditorAsync(http, ct).ConfigureAwait(false);

        if (editor.CheckGroup(name) is not PermissionEditRefusal.None and var refusal)
            return PermissionResults.Refused(refusal, editor, name);

        return PermissionResults.Changed(
            await Directory.DeleteGroupAsync(name, Actor(http), ct).ConfigureAwait(false)
        );
    }

    private async Task<IResult> SetNodeAsync(
        HttpContext http,
        string name,
        SetPermissionNodeRequest request,
        CancellationToken ct
    )
    {
        var editor = await EditorAsync(http, ct).ConfigureAwait(false);
        var node = request.Node?.Trim() ?? string.Empty;

        if (Check(editor, name, node) is { } refused)
            return refused;

        if (!PermissionResults.TryExpiry(request.Duration, UtcNow, out var expiresAt))
            return PermissionResults.BadDuration(request.Duration);

        return PermissionResults.Changed(
            await Directory
                .SetNodeAsync(
                    name,
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
        string name,
        string? node,
        bool? temporary,
        CancellationToken ct
    )
    {
        var editor = await EditorAsync(http, ct).ConfigureAwait(false);

        node = node?.Trim() ?? string.Empty;

        if (Check(editor, name, node) is { } refused)
            return refused;

        return PermissionResults.Changed(
            await Directory
                .UnsetNodeAsync(name, node, temporary ?? false, Actor(http), ct)
                .ConfigureAwait(false)
        );
    }

    private async Task<IResult> SetMetaAsync(
        HttpContext http,
        string name,
        SetPermissionMetaRequest request,
        CancellationToken ct
    )
    {
        var editor = await EditorAsync(http, ct).ConfigureAwait(false);

        if (editor.CheckGroup(name) is not PermissionEditRefusal.None and var refusal)
            return PermissionResults.Refused(refusal, editor, name);

        if (!PermissionResults.TryExpiry(request.Duration, UtcNow, out var expiresAt))
            return PermissionResults.BadDuration(request.Duration);

        return PermissionResults.Changed(
            await Directory
                .SetMetaAsync(
                    name,
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
        string name,
        string? key,
        bool? temporary,
        CancellationToken ct
    )
    {
        var editor = await EditorAsync(http, ct).ConfigureAwait(false);

        if (editor.CheckGroup(name) is not PermissionEditRefusal.None and var refusal)
            return PermissionResults.Refused(refusal, editor, name);

        return PermissionResults.Changed(
            await Directory
                .UnsetMetaAsync(
                    name,
                    key?.Trim() ?? string.Empty,
                    temporary ?? false,
                    Actor(http),
                    ct
                )
                .ConfigureAwait(false)
        );
    }

    private async Task<IResult> AddParentAsync(
        HttpContext http,
        string name,
        PermissionParentRequest request,
        CancellationToken ct
    )
    {
        var editor = await EditorAsync(http, ct).ConfigureAwait(false);
        var parent = request.Parent?.Trim().ToLowerInvariant() ?? string.Empty;

        // Inheriting a group hands out everything it holds: it has to be the editor's too.
        if (CheckBoth(editor, name, parent) is { } refused)
            return refused;

        return PermissionResults.Changed(
            await Directory.AddParentAsync(name, parent, Actor(http), ct).ConfigureAwait(false)
        );
    }

    private async Task<IResult> RemoveParentAsync(
        HttpContext http,
        string name,
        string parent,
        CancellationToken ct
    )
    {
        var editor = await EditorAsync(http, ct).ConfigureAwait(false);

        if (CheckBoth(editor, name, parent) is { } refused)
            return refused;

        return PermissionResults.Changed(
            await Directory.RemoveParentAsync(name, parent, Actor(http), ct).ConfigureAwait(false)
        );
    }

    /// <summary>The group is the editor's to change, and the node theirs to grant or deny.</summary>
    private static IResult? Check(PermissionEditor editor, string group, string node) =>
        editor.CheckGroup(group) is not PermissionEditRefusal.None and var refusal
            ? PermissionResults.Refused(refusal, editor, group)
        : editor.CheckAssignment(node) is not PermissionEditRefusal.None and var notHeld
            ? PermissionResults.Refused(notHeld, editor, node)
        : null;

    private static IResult? CheckBoth(PermissionEditor editor, string group, string other) =>
        editor.CheckGroup(group) is not PermissionEditRefusal.None and var refusal
            ? PermissionResults.Refused(refusal, editor, group)
        : editor.CheckGroup(other) is not PermissionEditRefusal.None and var heavy
            ? PermissionResults.Refused(heavy, editor, other)
        : null;

    private Task<PermissionEditor> EditorAsync(HttpContext http, CancellationToken ct) =>
        permissions.EditorForAsync(AdminIdentity.Of(http).PlayerId, ct);

    private static PlayerId Actor(HttpContext http) => AdminIdentity.Of(http).PlayerId;
}
