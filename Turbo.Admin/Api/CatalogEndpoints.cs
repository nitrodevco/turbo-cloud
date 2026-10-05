using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Orleans;
using Turbo.Admin.Api.Contracts;
using Turbo.Admin.Catalog;
using Turbo.Primitives.Catalog.Editing;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Admin.Api;

/// <summary>
/// The catalog editor: reading the tree, a page and the furniture, for staff with
/// <c>admin.catalog.view</c>; changing pages and offers and publishing, for those who also hold
/// <c>catalog.manage</c>. The changes are <see cref="ICatalogEditService"/>'s, which checks them.
/// </summary>
internal sealed class CatalogEndpoints(
    IGrainFactory grainFactory,
    AdminCatalogQueries catalog,
    ICatalogEditService editor
)
{
    private const string NO_ACCESS = "You can't see the catalog.";
    private const string NO_MANAGE = "You can't change the catalog.";

    public void Map(RouteGroupBuilder secured)
    {
        var group = secured.MapGroup("/catalog").AddEndpointFilter(RequireViewAsync);

        group.MapGet("/", TreeAsync);
        group.MapGet("/pages/{id:int}", PageAsync);
        group.MapGet("/furniture", (string? q) => Results.Ok(catalog.SearchFurniture(q)));
        group.MapPost("/pages", CreatePageAsync);
        group.MapPut("/pages/{id:int}", UpdatePageAsync);
        group.MapPost("/pages/{id:int}/move", MovePageAsync);
        group.MapDelete("/pages/{id:int}", DeletePageAsync);
        group.MapPost("/offers", CreateOfferAsync);
        group.MapPut("/offers/{id:int}", UpdateOfferAsync);
        group.MapDelete("/offers/{id:int}", DeleteOfferAsync);
        group.MapPut("/offers/{id:int}/limited", SaveLimitedAsync);
        group.MapDelete("/offers/{id:int}/limited", RemoveLimitedAsync);
        group.MapPost("/publish", PublishAsync);
    }

    private async Task<IResult> TreeAsync(string? type, HttpContext http, CancellationToken ct)
    {
        var kind = string.Equals(type, "builders", StringComparison.OrdinalIgnoreCase)
            ? CatalogType.BuildersClub
            : CatalogType.Normal;

        return Results.Ok(
            await catalog
                .GetTreeAsync(kind, await CanManageAsync(http, ct).ConfigureAwait(false), ct)
                .ConfigureAwait(false)
        );
    }

    private async Task<IResult> PageAsync(int id, CancellationToken ct) =>
        await catalog.GetPageAsync(id, ct).ConfigureAwait(false) is { } page
            ? Results.Ok(page)
            : AdminResults.Error(StatusCodes.Status404NotFound, "That page is gone.");

    private Task<IResult> CreatePageAsync(
        CatalogPageRequest request,
        HttpContext http,
        CancellationToken ct
    ) =>
        ManageAsync(
            http,
            ct,
            who =>
                request.ParentId is { } parentId
                    ? editor.CreatePageAsync(who, parentId, Draft(request), ct)
                    : Task.FromResult(CatalogEditResult.Refused("Say which page to put it under."))
        );

    private Task<IResult> UpdatePageAsync(
        int id,
        CatalogPageRequest request,
        HttpContext http,
        CancellationToken ct
    ) => ManageAsync(http, ct, who => editor.UpdatePageAsync(who, id, Draft(request), ct));

    private Task<IResult> MovePageAsync(
        int id,
        CatalogMoveRequest request,
        HttpContext http,
        CancellationToken ct
    ) =>
        ManageAsync(
            http,
            ct,
            who => editor.MovePageAsync(who, id, request.ParentId, request.Index, ct)
        );

    private Task<IResult> DeletePageAsync(int id, HttpContext http, CancellationToken ct) =>
        ManageAsync(http, ct, who => editor.DeletePageAsync(who, id, ct));

    private Task<IResult> CreateOfferAsync(
        CatalogOfferRequest request,
        HttpContext http,
        CancellationToken ct
    ) =>
        ManageAsync(
            http,
            ct,
            who =>
                Draft(request) is { } draft
                    ? editor.CreateOfferAsync(who, draft, ct)
                    : Task.FromResult(UnknownType)
        );

    private Task<IResult> UpdateOfferAsync(
        int id,
        CatalogOfferRequest request,
        HttpContext http,
        CancellationToken ct
    ) =>
        ManageAsync(
            http,
            ct,
            who =>
                Draft(request) is { } draft
                    ? editor.UpdateOfferAsync(who, id, draft, ct)
                    : Task.FromResult(UnknownType)
        );

    private Task<IResult> DeleteOfferAsync(int id, HttpContext http, CancellationToken ct) =>
        ManageAsync(http, ct, who => editor.DeleteOfferAsync(who, id, ct));

    private Task<IResult> SaveLimitedAsync(
        int id,
        CatalogLimitedRequest request,
        HttpContext http,
        CancellationToken ct
    ) =>
        ManageAsync(
            http,
            ct,
            who =>
                editor.SaveLimitedAsync(
                    who,
                    id,
                    new CatalogLimitedDraft(
                        request.TotalQuantity,
                        request.RaffleWindowSeconds,
                        Utc(request.StartsAtUtc),
                        Utc(request.EndsAtUtc),
                        request.Active
                    ),
                    ct
                )
        );

    private Task<IResult> RemoveLimitedAsync(int id, HttpContext http, CancellationToken ct) =>
        ManageAsync(http, ct, who => editor.RemoveLimitedAsync(who, id, ct));

    /// <summary>A time from the panel as UTC: it sends ISO times with their offset.</summary>
    private static DateTime? Utc(DateTime? at) => at?.ToUniversalTime();

    private async Task<IResult> PublishAsync(HttpContext http, CancellationToken ct)
    {
        if (!await CanManageAsync(http, ct).ConfigureAwait(false))
            return AdminResults.Error(StatusCodes.Status403Forbidden, NO_MANAGE);

        var result = await editor
            .PublishAsync(AdminIdentity.Of(http).PlayerId, ct)
            .ConfigureAwait(false);

        return Results.Ok(
            new CatalogPublishResponse(result.Pages, result.Offers, result.PlayersTold)
        );
    }

    private static readonly CatalogEditResult UnknownType = CatalogEditResult.Refused(
        "An offer gives a floor item, a wall item, a badge or a membership."
    );

    /// <summary>An edit by someone who holds <c>catalog.manage</c>: saved, or why it was not.</summary>
    private async Task<IResult> ManageAsync(
        HttpContext http,
        CancellationToken ct,
        Func<PlayerId, Task<CatalogEditResult>> edit
    )
    {
        if (!await CanManageAsync(http, ct).ConfigureAwait(false))
            return AdminResults.Error(StatusCodes.Status403Forbidden, NO_MANAGE);

        var result = await edit(AdminIdentity.Of(http).PlayerId).ConfigureAwait(false);

        return result.Saved
            ? Results.Ok(new CatalogSavedResponse(result.Id, editor.UnpublishedChanges))
            : AdminResults.Error(StatusCodes.Status400BadRequest, result.Error ?? "Not saved.");
    }

    private Task<bool> CanManageAsync(HttpContext http, CancellationToken ct) =>
        grainFactory.HasPermissionAsync(
            AdminIdentity.Of(http).PlayerId,
            PermissionNodes.Catalog.MANAGE,
            ct
        );

    private static CatalogPageDraft Draft(CatalogPageRequest request) =>
        new(
            request.Localization ?? string.Empty,
            request.Name,
            request.Icon,
            request.Layout ?? string.Empty,
            AdminCatalogQueries.Lines(request.ImageData),
            AdminCatalogQueries.Lines(request.TextData),
            request.Visible
        );

    /// <summary>The offer as the service takes it; null when the product type is not one it knows.</summary>
    private static CatalogOfferDraft? Draft(CatalogOfferRequest request)
    {
        CatalogProductDraft? product = null;

        if (request.Product is { } given)
        {
            if (AdminCatalogQueries.TypeOf(given.Type) is not { } type)
                return null;

            SubscriptionType? subscription = Enum.TryParse<SubscriptionType>(
                given.Subscription,
                ignoreCase: true,
                out var parsed
            )
                ? parsed
                : null;

            product = new CatalogProductDraft(
                type,
                given.DefinitionId,
                given.ExtraParam,
                given.Quantity,
                subscription,
                given.SubscriptionDays
            );
        }

        return new CatalogOfferDraft(
            request.PageId,
            request.LocalizationId ?? string.Empty,
            request.CostCredits,
            request.CostCurrency,
            request.CurrencyTypeId,
            request.CanGift,
            request.CanBundle,
            request.ClubLevel,
            request.Visible,
            product,
            request.ClubGiftDaysRequired
        );
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
                PermissionNodes.Admin.CATALOG_VIEW,
                http.RequestAborted
            )
            .ConfigureAwait(false)
            ? await next(context).ConfigureAwait(false)
            : AdminResults.Error(StatusCodes.Status403Forbidden, NO_ACCESS);
    }
}
