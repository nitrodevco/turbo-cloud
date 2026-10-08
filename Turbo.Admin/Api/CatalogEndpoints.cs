using System;
using System.Collections.Generic;
using System.Linq;
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
/// The catalog editor: reading the tree, a page, the front page's featured items and the
/// furniture, for staff with <c>admin.catalog.view</c>; changing pages, offers and featured items
/// and publishing, for those who also hold <c>catalog.manage</c>. The changes are <see cref="ICatalogEditService"/>'s, which checks them.
/// </summary>
internal sealed class CatalogEndpoints(
    IGrainFactory grainFactory,
    AdminCatalogQueries catalog,
    ICatalogEditService editor,
    AdminCatalogBuilder builder
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
        group.MapPost("/offers/{id:int}/move", MoveOfferAsync);
        group.MapDelete("/offers/{id:int}", DeleteOfferAsync);
        group.MapPut("/offers/{id:int}/limited", SaveLimitedAsync);
        group.MapDelete("/offers/{id:int}/limited", RemoveLimitedAsync);
        group.MapGet("/featured", FeaturedAsync);
        group.MapPut("/featured", SaveFeaturedAsync);
        group.MapPost("/publish", PublishAsync);
        group.MapGet("/builders/furni-lines", FurniLinesAsync);
        group.MapPost("/pages/{id:int}/build/preview", PreviewBuildAsync);
        group.MapPost("/pages/{id:int}/build", BuildAsync);
    }

    private async Task<IResult> TreeAsync(HttpContext http, CancellationToken ct) =>
        Results.Ok(
            await catalog
                .GetTreeAsync(await CanManageAsync(http, ct).ConfigureAwait(false), ct)
                .ConfigureAwait(false)
        );

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
                request.ParentId is not { } parentId
                    ? Task.FromResult(CatalogEditResult.Refused("Say which page to put it under."))
                : Draft(request) is { } draft ? editor.CreatePageAsync(who, parentId, draft, ct)
                : Task.FromResult(UnknownDisplay)
        );

    private Task<IResult> UpdatePageAsync(
        int id,
        CatalogPageRequest request,
        HttpContext http,
        CancellationToken ct
    ) =>
        ManageAsync(
            http,
            ct,
            who =>
                Draft(request) is { } draft
                    ? editor.UpdatePageAsync(who, id, draft, ct)
                    : Task.FromResult(UnknownDisplay)
        );

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

    private Task<IResult> MoveOfferAsync(
        int id,
        CatalogOfferMoveRequest request,
        HttpContext http,
        CancellationToken ct
    ) =>
        ManageAsync(
            http,
            ct,
            who => editor.MoveOfferAsync(who, id, request.PageId, request.Index, ct)
        );

    private Task<IResult> DeleteOfferAsync(int id, HttpContext http, CancellationToken ct) =>
        ManageAsync(http, ct, who => editor.DeleteOfferAsync(who, id, ct));

    private async Task<IResult> FeaturedAsync(CancellationToken ct) =>
        Results.Ok(await catalog.GetFeaturedAsync(ct).ConfigureAwait(false));

    private Task<IResult> SaveFeaturedAsync(
        CatalogFeaturedRequest request,
        HttpContext http,
        CancellationToken ct
    ) =>
        ManageAsync(
            http,
            ct,
            who =>
                Draft(request) is { } items
                    ? editor.SaveFeaturedItemsAsync(who, items, ct)
                    : Task.FromResult(UnknownFeaturedType)
        );

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

    private async Task<IResult> FurniLinesAsync(CancellationToken ct) =>
        Results.Ok(await builder.GetFurniLinesAsync(ct).ConfigureAwait(false));

    private async Task<IResult> PreviewBuildAsync(
        int id,
        CatalogBuildRequest request,
        CancellationToken ct
    ) => Built(await builder.PreviewAsync(id, request, ct).ConfigureAwait(false));

    private async Task<IResult> BuildAsync(
        int id,
        CatalogBuildRequest request,
        HttpContext http,
        CancellationToken ct
    )
    {
        if (!await CanManageAsync(http, ct).ConfigureAwait(false))
            return AdminResults.Error(StatusCodes.Status403Forbidden, NO_MANAGE);

        return Built(
            await builder
                .ApplyAsync(AdminIdentity.Of(http).PlayerId, id, request, ct)
                .ConfigureAwait(false)
        );
    }

    /// <summary>A page builder's plan or result, or why there is none.</summary>
    private static IResult Built<T>(CatalogBuildOutcome<T> outcome)
        where T : class =>
        outcome.Value is { } value
            ? Results.Ok(value)
            : AdminResults.Error(StatusCodes.Status400BadRequest, outcome.Error ?? "Not built.");

    private static readonly CatalogEditResult UnknownDisplay = CatalogEditResult.Refused(
        "A page is shown in the regular catalog, the Builders Club catalog, both, or neither."
    );

    private static readonly CatalogEditResult UnknownType = CatalogEditResult.Refused(
        "An offer gives floor and wall items, badges, effects, bots, pets or a membership."
    );

    private static readonly CatalogEditResult UnknownFeaturedType = CatalogEditResult.Refused(
        "A featured item opens a page, an offer or a product."
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

    /// <summary>The page as the service takes it; null when the display is not one it knows.</summary>
    private static CatalogPageDraft? Draft(CatalogPageRequest request) =>
        CatalogPageDisplayExtensions.FromName(request.Display) is { } display
            ? new(
                request.Localization ?? string.Empty,
                request.Name,
                request.Icon,
                request.Layout ?? string.Empty,
                AdminCatalogQueries.Lines(request.ImageData),
                AdminCatalogQueries.Lines(request.TextData),
                display
            )
            : null;

    /// <summary>The offer as the service takes it; null when a product type is not one it knows.</summary>
    private static CatalogOfferDraft? Draft(CatalogOfferRequest request)
    {
        CatalogProductDraft? product = null;
        List<CatalogProductDraft>? products = null;

        // The list replaces what the offer gives, so the one product is not read beside it.
        if (request.Products is { } list)
        {
            products = [.. list.Select(x => Draft(x)).OfType<CatalogProductDraft>()];

            if (products.Count != list.Length)
                return null;
        }
        else if (request.Product is { } given)
        {
            if (Draft(given) is not { } one)
                return null;

            product = one;
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
            request.ClubGiftDaysRequired,
            products
        );
    }

    /// <summary>A product as the service takes it; null when its type is not one it knows.</summary>
    private static CatalogProductDraft? Draft(CatalogProductRequest? given)
    {
        if (given is null || AdminCatalogQueries.TypeOf(given.Type) is not { } type)
            return null;

        SubscriptionType? subscription = Enum.TryParse<SubscriptionType>(
            given.Subscription,
            ignoreCase: true,
            out var parsed
        )
            ? parsed
            : null;

        return new CatalogProductDraft(
            type,
            given.DefinitionId,
            given.ExtraParam,
            given.Quantity,
            subscription,
            given.SubscriptionDays
        );
    }

    /// <summary>The featured items as the service takes them; null when one opens nothing it knows.</summary>
    private static List<CatalogFeaturedItemDraft>? Draft(CatalogFeaturedRequest request)
    {
        var items = new List<CatalogFeaturedItemDraft>();

        foreach (var item in request.Items ?? [])
        {
            if (AdminCatalogQueries.FeaturedTypeOf(item?.Type) is not { } type)
                return null;

            items.Add(
                new CatalogFeaturedItemDraft(
                    item!.Title ?? string.Empty,
                    item.Image ?? string.Empty,
                    type,
                    item.Value ?? string.Empty,
                    Utc(item.ExpiresAtUtc)
                )
            );
        }

        return items;
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
