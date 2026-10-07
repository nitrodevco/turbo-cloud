using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Orleans;
using Turbo.Admin.Api.Contracts;
using Turbo.Admin.Catalog;
using Turbo.Primitives.Figures;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Gamedata.Enums;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Admin.Api;

/// <summary>
/// The hotel's gamedata: Habbo's releases and what taking one in would change, the furniture
/// definitions as the client's furnidata has them, the files clients are sent and the history of
/// changes - for staff with <c>admin.gamedata.view</c>. Checking Habbo, importing, editing,
/// rebuilding and rolling back need <c>gamedata.manage</c> as well.
/// </summary>
internal sealed class GamedataEndpoints(
    IGrainFactory grainFactory,
    IHabboReleaseService releases,
    IGamedataFurnitureService furniture,
    IGamedataFileService files,
    IGamedataHistoryService history,
    IGamedataImportJobs imports,
    IGamedataTextService texts,
    IGamedataProductService products,
    IGamedataFigureService figures,
    IPlayerClothingService clothing,
    AdminCatalogQueries catalog,
    ILogger<GamedataEndpoints> logger
)
{
    private const string NO_ACCESS = "You can't see the gamedata.";
    private const string NO_MANAGE = "You can't change the gamedata.";
    private const string NO_RELEASE = "There is no such Habbo release. Check Habbo first.";
    private const string NO_PRODUCTS =
        "There is no such version of Habbo's product data. Check Habbo first.";
    private const string NO_TEXTS = "There is no such version of Habbo's texts. Check Habbo first.";
    private const string NO_FIGURES =
        "There is no such version of Habbo's figure data. Check Habbo first.";

    public void Map(RouteGroupBuilder secured)
    {
        var group = secured.MapGroup("/gamedata").AddEndpointFilter(RequireViewAsync);

        group.MapGet("/", StatusAsync);
        group.MapPost("/habbo/check", CheckAsync);
        group.MapGet("/habbo/import", PreviewAsync);
        group.MapPost("/habbo/import/{releaseId:int}", ImportAsync);
        group.MapGet("/habbo/import/job", () => Results.Ok(new { job = imports.Current }));
        group.MapGet("/furniture", (string? q) => Results.Ok(catalog.SearchFurniture(q)));
        group.MapGet("/furniture/{id:int}", DefinitionAsync);
        group.MapPut("/furniture/{id:int}", UpdateDefinitionAsync);
        group.MapGet("/furniture/habbo-values", HabboValuesPreviewAsync);
        group.MapPost("/furniture/habbo-values", TakeHabboValuesAsync);
        group.MapPost("/files/{file}/build", RebuildAsync);
        group.MapGet("/texts/import", TextPreviewAsync);
        group.MapPost("/texts/import/{versionId:int}", TextImportAsync);
        group.MapGet(
            "/texts",
            (string? q, int? page, CancellationToken ct) => TextSearchAsync(q, page, ct)
        );
        group.MapPut("/texts", SaveTextAsync);
        group.MapDelete("/texts", DeleteTextAsync);
        group.MapGet("/products/import", ProductPreviewAsync);
        group.MapPost("/products/import/{versionId:int}", ProductImportAsync);
        group.MapGet(
            "/products",
            (string? q, int? page, CancellationToken ct) => ProductSearchAsync(q, page, ct)
        );
        group.MapPut("/products", SaveProductAsync);
        group.MapDelete("/products", DeleteProductAsync);
        group.MapGet("/figures/import", FigurePreviewAsync);
        group.MapPost("/figures/import/{versionId:int}", FigureImportAsync);
        group.MapGet(
            "/figures",
            (
                FigureRecordKind kind,
                string? group,
                string? q,
                string[]? has,
                int? page,
                CancellationToken ct
            ) => FigureSearchAsync(kind, group, q, has, page, ct)
        );
        group.MapGet("/figures/kinds", FigureKindsAsync);
        group.MapPut("/figures", SaveFigureAsync);
        group.MapGet("/figures/palettes", PalettesAsync);
        group.MapPut("/figures/batch", SaveFigureBatchAsync);
        group.MapDelete("/figures", DeleteFigureAsync);
        group.MapGet("/figures/owned/{playerId:int}", OwnedClothingAsync);
        group.MapPost("/figures/owned/{playerId:int}", GrantClothingAsync);
        group.MapPost("/figures/owned/{playerId:int}/revoke", RevokeClothingAsync);

        // The catalog's offers name products, so the catalog editor reads them too.
        secured
            .MapGroup("/product-data")
            .AddEndpointFilter(RequireCatalogOrGamedataAsync)
            .MapGet("/", ProductLookupAsync);
        group.MapGet("/history", (int? page, CancellationToken ct) => HistoryAsync(page, ct));
        group.MapGet("/history/{id:int}", ChangesAsync);
        group.MapPost("/history/{id:int}/rollback", RollbackAsync);
    }

    private async Task<IResult> StatusAsync(HttpContext http, CancellationToken ct)
    {
        var latest = await releases.GetLatestAsync(ct).ConfigureAwait(false);
        var latestTexts = await releases.GetLatestTextsAsync(ct).ConfigureAwait(false);
        var latestProducts = await releases.GetLatestProductsAsync(ct).ConfigureAwait(false);
        var latestFigures = await releases.GetLatestFiguresAsync(ct).ConfigureAwait(false);
        var figureData = await files
            .GetCurrentAsync(GamedataFiles.FIGURE_DATA, ct)
            .ConfigureAwait(false);
        var productData = await files
            .GetCurrentAsync(GamedataFiles.PRODUCT_DATA, ct)
            .ConfigureAwait(false);
        var furnitureData = await files
            .GetCurrentAsync(GamedataFiles.FURNITURE_DATA, ct)
            .ConfigureAwait(false);
        var externalTexts = await files
            .GetCurrentAsync(GamedataFiles.EXTERNAL_TEXTS, ct)
            .ConfigureAwait(false);

        return Results.Ok(
            new GamedataStatusResponse(
                latest,
                latestTexts,
                latestProducts,
                latestFigures,
                furnitureData.File,
                externalTexts.File,
                productData.File,
                figureData.File,
                await CanManageAsync(http, ct).ConfigureAwait(false)
            )
        );
    }

    private Task<IResult> CheckAsync(HttpContext http, CancellationToken ct) =>
        ManageAsync(
            http,
            ct,
            async () =>
            {
                try
                {
                    return Results.Ok(await releases.CheckAsync(ct).ConfigureAwait(false));
                }
                catch (HttpRequestException ex)
                {
                    logger.LogWarning(
                        ex,
                        "Checking Habbo from the panel failed: {Message}",
                        ex.Message
                    );

                    return AdminResults.Error(
                        StatusCodes.Status502BadGateway,
                        $"Habbo could not be checked: {ex.Message}"
                    );
                }
            }
        );

    private async Task<IResult> PreviewAsync(int? releaseId, CancellationToken ct) =>
        await furniture.PreviewImportAsync(releaseId, ct).ConfigureAwait(false) is { } preview
            ? Results.Ok(preview)
            : AdminResults.Error(StatusCodes.Status404NotFound, NO_RELEASE);

    private Task<IResult> ImportAsync(int releaseId, HttpContext http, CancellationToken ct) =>
        ManageAsync(
            http,
            ct,
            async () =>
            {
                try
                {
                    return
                        await imports
                            .StartAsync(releaseId, AdminIdentity.Of(http).PlayerId, ct)
                            .ConfigureAwait(false)
                            is { } job
                        ? Results.Ok(new { job })
                        : AdminResults.Error(StatusCodes.Status404NotFound, NO_RELEASE);
                }
                catch (InvalidOperationException ex)
                {
                    return AdminResults.Error(StatusCodes.Status409Conflict, ex.Message);
                }
            }
        );

    private async Task<IResult> DefinitionAsync(int id, CancellationToken ct) =>
        await furniture.GetDefinitionAsync(id, ct).ConfigureAwait(false) is { } definition
            ? Results.Ok(definition)
            : Results.NotFound();

    private Task<IResult> UpdateDefinitionAsync(
        int id,
        FurnitureDefinitionUpdateRequest request,
        HttpContext http,
        CancellationToken ct
    ) =>
        ManageAsync(
            http,
            ct,
            async () =>
            {
                if (request.Fields is not { Count: > 0 } fields)
                    return AdminResults.Error(
                        StatusCodes.Status400BadRequest,
                        "Nothing to change."
                    );

                try
                {
                    return
                        await furniture
                            .UpdateDefinitionAsync(id, fields, AdminIdentity.Of(http).PlayerId, ct)
                            .ConfigureAwait(false)
                            is { } definition
                        ? Results.Ok(definition)
                        : Results.NotFound();
                }
                catch (ArgumentException ex)
                {
                    return AdminResults.Error(StatusCodes.Status400BadRequest, ex.Message);
                }
            }
        );

    private async Task<IResult> HabboValuesPreviewAsync(string? fields, CancellationToken ct)
    {
        try
        {
            return Results.Ok(
                await furniture.PreviewHabboValuesAsync(FieldList(fields), ct).ConfigureAwait(false)
            );
        }
        catch (ArgumentException ex)
        {
            return AdminResults.Error(StatusCodes.Status400BadRequest, ex.Message);
        }
    }

    private Task<IResult> TakeHabboValuesAsync(
        HabboValuesRequest request,
        HttpContext http,
        CancellationToken ct
    ) =>
        ManageAsync(
            http,
            ct,
            async () =>
            {
                try
                {
                    var changeSet = await furniture
                        .TakeHabboValuesAsync(
                            request.Fields ?? [],
                            AdminIdentity.Of(http).PlayerId,
                            ct
                        )
                        .ConfigureAwait(false);

                    return Results.Ok(new { changeSet });
                }
                catch (ArgumentException ex)
                {
                    return AdminResults.Error(StatusCodes.Status400BadRequest, ex.Message);
                }
            }
        );

    /// <summary>A comma-separated list of furnidata keys from the query.</summary>
    private static string[] FieldList(string? fields) =>
        (fields ?? string.Empty).Split(
            ',',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
        );

    private Task<IResult> RebuildAsync(string file, HttpContext http, CancellationToken ct) =>
        ManageAsync(
            http,
            ct,
            async () =>
                GamedataFiles.IsKnown(file)
                    ? Results.Ok((await files.RebuildAsync(file, ct).ConfigureAwait(false)).File)
                    : Results.NotFound()
        );

    private async Task<IResult> TextPreviewAsync(int? versionId, CancellationToken ct) =>
        await texts.PreviewImportAsync(versionId, ct).ConfigureAwait(false) is { } preview
            ? Results.Ok(preview)
            : AdminResults.Error(StatusCodes.Status404NotFound, NO_TEXTS);

    private Task<IResult> TextImportAsync(int versionId, HttpContext http, CancellationToken ct) =>
        ManageAsync(
            http,
            ct,
            async () =>
            {
                try
                {
                    return
                        await imports
                            .StartTextsAsync(versionId, AdminIdentity.Of(http).PlayerId, ct)
                            .ConfigureAwait(false)
                            is { } job
                        ? Results.Ok(new { job })
                        : AdminResults.Error(StatusCodes.Status404NotFound, NO_TEXTS);
                }
                catch (InvalidOperationException ex)
                {
                    return AdminResults.Error(StatusCodes.Status409Conflict, ex.Message);
                }
            }
        );

    private async Task<IResult> ProductPreviewAsync(int? versionId, CancellationToken ct) =>
        await products.PreviewImportAsync(versionId, ct).ConfigureAwait(false) is { } preview
            ? Results.Ok(preview)
            : AdminResults.Error(StatusCodes.Status404NotFound, NO_PRODUCTS);

    private Task<IResult> ProductImportAsync(
        int versionId,
        HttpContext http,
        CancellationToken ct
    ) =>
        ManageAsync(
            http,
            ct,
            async () =>
            {
                try
                {
                    return
                        await imports
                            .StartProductsAsync(versionId, AdminIdentity.Of(http).PlayerId, ct)
                            .ConfigureAwait(false)
                            is { } job
                        ? Results.Ok(new { job })
                        : AdminResults.Error(StatusCodes.Status404NotFound, NO_PRODUCTS);
                }
                catch (InvalidOperationException ex)
                {
                    return AdminResults.Error(StatusCodes.Status409Conflict, ex.Message);
                }
            }
        );

    private async Task<IResult> ProductSearchAsync(string? q, int? page, CancellationToken ct) =>
        Results.Ok(await products.SearchAsync(q, page ?? 0, ct).ConfigureAwait(false));

    /// <summary>
    /// The products of these codes (<c>?codes=a,b</c>) - what the catalog editor shows for its
    /// offers' name keys - or a page of those that hold the words (<c>?q=</c>), to pick one from.
    /// </summary>
    private async Task<IResult> ProductLookupAsync(
        string? codes,
        string? q,
        CancellationToken ct
    ) =>
        Results.Ok(
            string.IsNullOrWhiteSpace(q)
                ? await products.LookupAsync(FieldList(codes), ct).ConfigureAwait(false)
                : (await products.SearchAsync(q, 0, ct).ConfigureAwait(false)).Items
        );

    private Task<IResult> SaveProductAsync(
        ProductSaveRequest request,
        HttpContext http,
        CancellationToken ct
    ) =>
        ManageAsync(
            http,
            ct,
            async () =>
            {
                try
                {
                    return Results.Ok(
                        await products
                            .SaveAsync(
                                request.Code ?? string.Empty,
                                request.Name,
                                request.Description,
                                AdminIdentity.Of(http).PlayerId,
                                ct
                            )
                            .ConfigureAwait(false)
                    );
                }
                catch (ArgumentException ex)
                {
                    return AdminResults.Error(StatusCodes.Status400BadRequest, ex.Message);
                }
            }
        );

    private Task<IResult> DeleteProductAsync(
        string? code,
        HttpContext http,
        CancellationToken ct
    ) =>
        ManageAsync(
            http,
            ct,
            async () =>
                await products
                    .DeleteAsync(code ?? string.Empty, AdminIdentity.Of(http).PlayerId, ct)
                    .ConfigureAwait(false)
                    ? Results.NoContent()
                    : Results.NotFound()
        );

    private async Task<IResult> FigurePreviewAsync(int? versionId, CancellationToken ct) =>
        await figures.PreviewImportAsync(versionId, ct).ConfigureAwait(false) is { } preview
            ? Results.Ok(preview)
            : AdminResults.Error(StatusCodes.Status404NotFound, NO_FIGURES);

    private Task<IResult> FigureImportAsync(
        int versionId,
        HttpContext http,
        CancellationToken ct
    ) =>
        ManageAsync(
            http,
            ct,
            async () =>
            {
                try
                {
                    return
                        await imports
                            .StartFiguresAsync(versionId, AdminIdentity.Of(http).PlayerId, ct)
                            .ConfigureAwait(false)
                            is { } job
                        ? Results.Ok(new { job })
                        : AdminResults.Error(StatusCodes.Status404NotFound, NO_FIGURES);
                }
                catch (InvalidOperationException ex)
                {
                    return AdminResults.Error(StatusCodes.Status409Conflict, ex.Message);
                }
            }
        );

    private async Task<IResult> FigureSearchAsync(
        FigureRecordKind kind,
        string? group,
        string? q,
        string[]? has,
        int? page,
        CancellationToken ct
    ) =>
        Results.Ok(
            await figures
                .SearchAsync(kind, group, q, has ?? [], page ?? 0, ct)
                .ConfigureAwait(false)
        );

    private async Task<IResult> FigureKindsAsync(CancellationToken ct) =>
        Results.Ok(await figures.GetKindsAsync(ct).ConfigureAwait(false));

    private Task<IResult> SaveFigureAsync(
        FigureSaveRequest request,
        HttpContext http,
        CancellationToken ct
    ) =>
        ManageAsync(
            http,
            ct,
            async () =>
            {
                try
                {
                    return Results.Ok(
                        await figures
                            .SaveAsync(
                                request.Kind,
                                request.Data ?? string.Empty,
                                AdminIdentity.Of(http).PlayerId,
                                ct
                            )
                            .ConfigureAwait(false)
                    );
                }
                catch (ArgumentException ex)
                {
                    return AdminResults.Error(StatusCodes.Status400BadRequest, ex.Message);
                }
            }
        );

    private async Task<IResult> PalettesAsync(CancellationToken ct) =>
        Results.Ok(await figures.GetPalettesAsync(ct).ConfigureAwait(false));

    private Task<IResult> SaveFigureBatchAsync(
        FigureBatchRequest request,
        HttpContext http,
        CancellationToken ct
    ) =>
        ManageAsync(
            http,
            ct,
            async () =>
            {
                try
                {
                    var changed = await figures
                        .SaveBatchAsync(
                            request.Kind,
                            request.Save ?? [],
                            request.Delete ?? [],
                            request.Summary ?? string.Empty,
                            AdminIdentity.Of(http).PlayerId,
                            ct
                        )
                        .ConfigureAwait(false);

                    return Results.Ok(new { changed });
                }
                catch (ArgumentException ex)
                {
                    return AdminResults.Error(StatusCodes.Status400BadRequest, ex.Message);
                }
            }
        );

    private Task<IResult> DeleteFigureAsync(
        FigureRecordKind kind,
        string? key,
        HttpContext http,
        CancellationToken ct
    ) =>
        ManageAsync(
            http,
            ct,
            async () =>
                await figures
                    .DeleteAsync(kind, key ?? string.Empty, AdminIdentity.Of(http).PlayerId, ct)
                    .ConfigureAwait(false)
                    ? Results.NoContent()
                    : Results.NotFound()
        );

    private async Task<IResult> OwnedClothingAsync(int playerId, CancellationToken ct) =>
        Results.Ok(
            new
            {
                setIds = (await clothing.GetOwnedAsync(playerId, ct).ConfigureAwait(false))
                    .Order()
                    .ToArray(),
            }
        );

    private Task<IResult> GrantClothingAsync(
        int playerId,
        ClothingGrantRequest request,
        HttpContext http,
        CancellationToken ct
    ) =>
        ManageAsync(
            http,
            ct,
            async () =>
            {
                var granted = await clothing
                    .GrantAsync(playerId, request.SetIds ?? [], ct)
                    .ConfigureAwait(false);

                logger.LogInformation(
                    "Player {StaffId} gave player {PlayerId} {Count} figure sets: {SetIds}",
                    AdminIdentity.Of(http).PlayerId.Value,
                    playerId,
                    granted,
                    request.SetIds
                );

                return Results.Ok(new { changed = granted });
            }
        );

    private Task<IResult> RevokeClothingAsync(
        int playerId,
        ClothingGrantRequest request,
        HttpContext http,
        CancellationToken ct
    ) =>
        ManageAsync(
            http,
            ct,
            async () =>
            {
                var revoked = await clothing
                    .RevokeAsync(playerId, request.SetIds ?? [], ct)
                    .ConfigureAwait(false);

                logger.LogInformation(
                    "Player {StaffId} took {Count} figure sets from player {PlayerId}: {SetIds}",
                    AdminIdentity.Of(http).PlayerId.Value,
                    revoked,
                    playerId,
                    request.SetIds
                );

                return Results.Ok(new { changed = revoked });
            }
        );

    private async ValueTask<object?> RequireCatalogOrGamedataAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next
    )
    {
        var http = context.HttpContext;
        var player = AdminIdentity.Of(http).PlayerId;

        return
            await grainFactory
                .HasPermissionAsync(player, PermissionNodes.Admin.CATALOG_VIEW, http.RequestAborted)
                .ConfigureAwait(false)
            || await grainFactory
                .HasPermissionAsync(
                    player,
                    PermissionNodes.Admin.GAMEDATA_VIEW,
                    http.RequestAborted
                )
                .ConfigureAwait(false)
            ? await next(context).ConfigureAwait(false)
            : AdminResults.Error(StatusCodes.Status403Forbidden, NO_ACCESS);
    }

    private async Task<IResult> TextSearchAsync(string? q, int? page, CancellationToken ct) =>
        Results.Ok(await texts.SearchAsync(q, page ?? 0, ct).ConfigureAwait(false));

    private Task<IResult> SaveTextAsync(
        TextSaveRequest request,
        HttpContext http,
        CancellationToken ct
    ) =>
        ManageAsync(
            http,
            ct,
            async () =>
            {
                try
                {
                    return Results.Ok(
                        await texts
                            .SaveAsync(
                                request.Key ?? string.Empty,
                                request.Value ?? string.Empty,
                                AdminIdentity.Of(http).PlayerId,
                                ct
                            )
                            .ConfigureAwait(false)
                    );
                }
                catch (ArgumentException ex)
                {
                    return AdminResults.Error(StatusCodes.Status400BadRequest, ex.Message);
                }
            }
        );

    private Task<IResult> DeleteTextAsync(string? key, HttpContext http, CancellationToken ct) =>
        ManageAsync(
            http,
            ct,
            async () =>
                await texts
                    .DeleteAsync(key ?? string.Empty, AdminIdentity.Of(http).PlayerId, ct)
                    .ConfigureAwait(false)
                    ? Results.NoContent()
                    : Results.NotFound()
        );

    private async Task<IResult> HistoryAsync(int? page, CancellationToken ct) =>
        Results.Ok(await history.ListAsync(page ?? 0, ct).ConfigureAwait(false));

    private async Task<IResult> ChangesAsync(int id, CancellationToken ct) =>
        await history.GetChangesAsync(id, ct).ConfigureAwait(false) is { } changes
            ? Results.Ok(changes)
            : Results.NotFound();

    private Task<IResult> RollbackAsync(int id, HttpContext http, CancellationToken ct) =>
        ManageAsync(
            http,
            ct,
            async () =>
            {
                try
                {
                    return
                        await history
                            .RollbackAsync(id, AdminIdentity.Of(http).PlayerId, ct)
                            .ConfigureAwait(false)
                            is { } result
                        ? Results.Ok(result)
                        : Results.NotFound();
                }
                catch (InvalidOperationException ex)
                {
                    return AdminResults.Error(StatusCodes.Status409Conflict, ex.Message);
                }
            }
        );

    /// <summary>A change by someone who holds <c>gamedata.manage</c>.</summary>
    private async Task<IResult> ManageAsync(
        HttpContext http,
        CancellationToken ct,
        Func<Task<IResult>> change
    ) =>
        await CanManageAsync(http, ct).ConfigureAwait(false)
            ? await change().ConfigureAwait(false)
            : AdminResults.Error(StatusCodes.Status403Forbidden, NO_MANAGE);

    private Task<bool> CanManageAsync(HttpContext http, CancellationToken ct) =>
        grainFactory.HasPermissionAsync(
            AdminIdentity.Of(http).PlayerId,
            PermissionNodes.Gamedata.MANAGE,
            ct
        );

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
