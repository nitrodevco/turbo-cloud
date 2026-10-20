using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Orleans;
using Turbo.Primitives.Orleans;

namespace Turbo.Admin.Api;

/// <summary>
/// The permission an admin route needs, declared on the route or its group and checked by
/// <see cref="AdminSessionFilter"/> before the handler runs, the way <c>[RequiresPermission]</c>
/// is for a packet handler. A staff member must hold one of <see cref="AnyOf"/>; without it the
/// answer is 403 with <see cref="Refusal"/>. <see cref="ChangesOnly"/> holds it to the requests
/// that change something (anything but GET), so a group says "see with one node, change with
/// another" once. <see cref="OnRoute"/> tells a route's own requirement from its group's.
/// </summary>
internal sealed record AdminAccess(
    ImmutableArray<string> AnyOf,
    string Refusal,
    bool ChangesOnly,
    bool OnRoute
);

/// <summary>Why a route needs no more than the requirements around it.</summary>
internal enum AdminAccessNoteKind
{
    /// <summary>Only the panel itself (<c>admin.panel</c>): the staff member's own account, their session.</summary>
    PanelOnly,

    /// <summary>A request that changes nothing although it is not a GET (a check, a search, a preview).</summary>
    ViewIsEnough,

    /// <summary>
    /// The handler decides from the request (the target's rank, a command's own node, a plugin
    /// route's node), or logs each refusal, so it checks itself.
    /// </summary>
    ChecksInHandler,
}

internal sealed record AdminAccessNote(AdminAccessNoteKind Kind, string Reason);

/// <summary>Marks the routes behind <see cref="AdminSessionFilter"/>: every one declares its access.</summary>
internal sealed record AdminSessionRequired;

internal static class AdminAccessExtensions
{
    /// <summary>Every request to the group needs <paramref name="node"/>.</summary>
    public static RouteGroupBuilder RequirePermission(
        this RouteGroupBuilder group,
        string node,
        string refusal
    ) => group.WithMetadata(new AdminAccess([node], refusal, false, false));

    /// <summary>Every request to the group needs one of <paramref name="nodes"/>.</summary>
    public static RouteGroupBuilder RequireAnyPermission(
        this RouteGroupBuilder group,
        string refusal,
        params string[] nodes
    ) => group.WithMetadata(new AdminAccess([.. nodes], refusal, false, false));

    /// <summary>A request to the group that changes something (not a GET) needs <paramref name="node"/>.</summary>
    public static RouteGroupBuilder RequirePermissionToChange(
        this RouteGroupBuilder group,
        string node,
        string refusal
    ) => group.WithMetadata(new AdminAccess([node], refusal, true, false));

    /// <summary>The route needs <paramref name="node"/>, on top of what its group needs.</summary>
    public static RouteHandlerBuilder RequirePermission(
        this RouteHandlerBuilder route,
        string node,
        string refusal
    ) => route.WithMetadata(new AdminAccess([node], refusal, false, true));

    /// <summary>The route needs one of <paramref name="nodes"/>, on top of what its group needs.</summary>
    public static RouteHandlerBuilder RequireAnyPermission(
        this RouteHandlerBuilder route,
        string refusal,
        params string[] nodes
    ) => route.WithMetadata(new AdminAccess([.. nodes], refusal, false, true));

    /// <inheritdoc cref="AdminAccessNoteKind.PanelOnly"/>
    public static TBuilder PanelOnly<TBuilder>(this TBuilder builder, string reason)
        where TBuilder : IEndpointConventionBuilder =>
        builder.WithMetadata(new AdminAccessNote(AdminAccessNoteKind.PanelOnly, reason));

    /// <inheritdoc cref="AdminAccessNoteKind.ViewIsEnough"/>
    public static TBuilder ViewIsEnough<TBuilder>(this TBuilder builder, string reason)
        where TBuilder : IEndpointConventionBuilder =>
        builder.WithMetadata(new AdminAccessNote(AdminAccessNoteKind.ViewIsEnough, reason));

    /// <inheritdoc cref="AdminAccessNoteKind.ChecksInHandler"/>
    public static TBuilder ChecksInHandler<TBuilder>(this TBuilder builder, string reason)
        where TBuilder : IEndpointConventionBuilder =>
        builder.WithMetadata(new AdminAccessNote(AdminAccessNoteKind.ChecksInHandler, reason));

    /// <summary>
    /// Whether the staff member behind the request holds <paramref name="node"/>: for what a
    /// handler shows (a list that says whether it may be changed) or decides from the request,
    /// not for who may call it, which the route declares.
    /// </summary>
    public static Task<bool> HoldsAsync(
        this IGrainFactory grainFactory,
        HttpContext http,
        string node,
        CancellationToken ct
    ) => AdminIdentity.Of(http).HoldsAsync(grainFactory, node, ct);
}
