using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Orleans;
using Turbo.Admin.Api.Contracts;
using Turbo.Primitives.Hotel;
using Turbo.Primitives.Hotel.Enums;
using Turbo.Primitives.Hotel.Snapshots;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Admin.Api;

/// <summary>
/// What the reception's widgets show besides its variables (those are under
/// <see cref="GamedataEndpoints"/>): the promo articles. For staff with <c>admin.gamedata.view</c>,
/// as the hotel view is; changing them needs <c>gamedata.manage</c>.
/// </summary>
internal sealed class ReceptionEndpoints(IGrainFactory grainFactory, IPromoArticleService articles)
{
    private const string NO_ACCESS = "You can't see the hotel view.";
    private const string NO_MANAGE = "You can't change the hotel view.";

    public void Map(RouteGroupBuilder secured)
    {
        var group = secured.MapGroup("/hotel-view").AddEndpointFilter(RequireViewAsync);

        group.MapGet(
            "/articles",
            async (CancellationToken ct) =>
                Results.Ok(new { articles = await articles.ListAsync(ct).ConfigureAwait(false) })
        );
        group.MapPost(
            "/articles",
            (PromoArticleRequest request, HttpContext http, CancellationToken ct) =>
                SaveArticleAsync(0, request, http, ct)
        );
        group.MapPut("/articles/{id:int}", SaveArticleAsync);
        group.MapDelete("/articles/{id:int}", DeleteArticleAsync);
        group.MapPut("/articles/order", ReorderArticlesAsync);
    }

    private Task<IResult> SaveArticleAsync(
        int id,
        PromoArticleRequest request,
        HttpContext http,
        CancellationToken ct
    ) =>
        ManageAsync(
            http,
            ct,
            async () =>
                Results.Ok(
                    await articles
                        .SaveAsync(
                            new PromoArticleSnapshot
                            {
                                Id = id,
                                Title = request.Title ?? string.Empty,
                                BodyText = request.BodyText ?? string.Empty,
                                ButtonText = request.ButtonText ?? string.Empty,
                                LinkType = request.LinkType ?? PromoArticleLinkType.None,
                                LinkContent = request.LinkContent ?? string.Empty,
                                ImageUrl = request.ImageUrl ?? string.Empty,
                                SortOrder = 0,
                                Visible = request.Visible ?? true,
                                StartsAt = Utc(request.StartsAt),
                                EndsAt = Utc(request.EndsAt),
                            },
                            ct
                        )
                        .ConfigureAwait(false)
                )
        );

    private Task<IResult> DeleteArticleAsync(int id, HttpContext http, CancellationToken ct) =>
        ManageAsync(
            http,
            ct,
            async () =>
                await articles.DeleteAsync(id, ct).ConfigureAwait(false)
                    ? Results.NoContent()
                    : Results.NotFound()
        );

    private Task<IResult> ReorderArticlesAsync(
        ReorderRequest request,
        HttpContext http,
        CancellationToken ct
    ) =>
        ManageAsync(
            http,
            ct,
            async () =>
            {
                await articles.ReorderAsync(request.Ids ?? [], ct).ConfigureAwait(false);

                return Results.NoContent();
            }
        );

    /// <summary>A time the panel sent, as UTC: the panel sends UTC, marked or not.</summary>
    private static DateTime? Utc(DateTime? time) =>
        time is { } t
            ? t.Kind == DateTimeKind.Local
                ? t.ToUniversalTime()
                : DateTime.SpecifyKind(t, DateTimeKind.Utc)
            : null;

    /// <summary>
    /// A change by someone who holds <c>gamedata.manage</c>: what it answers, or 400 with why it
    /// was refused.
    /// </summary>
    private async Task<IResult> ManageAsync(
        HttpContext http,
        CancellationToken ct,
        Func<Task<IResult>> change
    )
    {
        if (
            !await grainFactory
                .HasPermissionAsync(
                    AdminIdentity.Of(http).PlayerId,
                    PermissionNodes.Gamedata.MANAGE,
                    ct
                )
                .ConfigureAwait(false)
        )
            return AdminResults.Error(StatusCodes.Status403Forbidden, NO_MANAGE);

        try
        {
            return await change().ConfigureAwait(false);
        }
        catch (ArgumentException ex)
        {
            return AdminResults.Error(StatusCodes.Status400BadRequest, ex.Message);
        }
    }

    private async ValueTask<object?> RequireViewAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next
    )
    {
        var http = context.HttpContext;

        return await grainFactory
            .HasPermissionAsync(
                AdminIdentity.Of(http).PlayerId,
                PermissionNodes.Admin.GAMEDATA_VIEW,
                http.RequestAborted
            )
            .ConfigureAwait(false)
            ? await next(context).ConfigureAwait(false)
            : AdminResults.Error(StatusCodes.Status403Forbidden, NO_ACCESS);
    }
}
