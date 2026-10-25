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
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Gamedata.Enums;
using Turbo.Primitives.Gamedata.Snapshots;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Rooms.Providers;

namespace Turbo.Admin.Api;

/// <summary>
/// The hotel's gamedata: Habbo's releases and what taking one in would change, the furniture
/// definitions as the client's furnidata has them, the client's external variables, the files
/// clients are sent and the history of changes - for staff with <c>admin.gamedata.view</c>. Checking Habbo, importing, editing,
/// rebuilding and rolling back need <c>gamedata.manage</c> as well.
/// </summary>
internal sealed class GamedataEndpoints(
    IGrainFactory grainFactory,
    IHabboReleaseService releases,
    IHabboClientService client,
    IHabboClientBuilds clientBuilds,
    IAssetSyncService assetSync,
    IGamedataFurnitureService furniture,
    IGamedataFileService files,
    IGamedataHistoryService history,
    IGamedataImportJobs imports,
    IGamedataTextService texts,
    IGamedataProductService products,
    IGamedataVariableService variables,
    IHabboVariableService habboVariables,
    IHotelViewService hotelView,
    IGamedataFigureService figures,
    IPlayerClothingService clothing,
    AdminCatalogQueries catalog,
    IRoomObjectLogicProvider logics,
    ILogger<GamedataEndpoints> logger
)
{
    private const string NO_ACCESS = "You can't see the gamedata.";
    private const string NO_MANAGE = "You can't change the gamedata.";
    private const string NO_RELEASE = "There is no such Habbo release. Check Habbo first.";
    private const string NO_PRODUCTS =
        "There is no such version of Habbo's product data. Check Habbo first.";
    private const string NO_TEXTS = "There is no such version of Habbo's texts. Check Habbo first.";
    private const string NO_VARIABLES =
        "There is no such version of Habbo's variables. Check Habbo first.";
    private const string NO_FIGURES =
        "There is no such version of Habbo's figure data. Check Habbo first.";

    public void Map(RouteGroupBuilder secured)
    {
        var group = secured
            .MapGroup("/gamedata")
            .RequirePermission(PermissionNodes.Admin.GAMEDATA_VIEW, NO_ACCESS)
            .RequirePermissionToChange(PermissionNodes.Gamedata.MANAGE, NO_MANAGE);

        group.MapGet("/", StatusAsync);
        group.MapPost("/habbo/check", CheckAsync);
        group.MapGet(
            "/habbo/client",
            async (CancellationToken ct) =>
                Results.Ok(await client.GetStatusAsync(ct).ConfigureAwait(false))
        );
        group.MapPost(
            "/habbo/client/check",
            async (HttpContext http, CancellationToken ct) =>
                Results.Ok(await client.CheckAsync(wait: false, ct).ConfigureAwait(false))
        );
        group.MapGet(
            "/habbo/client/build",
            async (CancellationToken ct) =>
                Results.Ok(await clientBuilds.GetAsync(ct).ConfigureAwait(false))
        );
        group.MapPost(
            "/habbo/client/build",
            (HttpContext http, CancellationToken ct) =>
            {
                try
                {
                    return Task.FromResult(
                        Results.Ok(
                            AssetJobResponse.From(
                                assetSync.StartClientBuild(
                                    AdminIdentity.Of(http).PlayerId,
                                    force: true
                                )
                            )
                        )
                    );
                }
                catch (InvalidOperationException ex)
                {
                    return Task.FromResult(
                        AdminResults.Error(StatusCodes.Status409Conflict, ex.Message)
                    );
                }
            }
        );
        group.MapDelete(
            "/habbo/client/releases/{version}",
            async (string version, HttpContext http, CancellationToken ct) =>
                await client.ForgetAsync(version, ct).ConfigureAwait(false)
                    ? Results.NoContent()
                    : AdminResults.Error(
                        StatusCodes.Status404NotFound,
                        "There is no such release, or it is downloading."
                    )
        );
        group.MapGet("/habbo/import", PreviewAsync);
        group.MapPost("/habbo/import/{releaseId:int}", ImportAsync);
        group.MapGet("/habbo/import/job", () => Results.Ok(new { job = imports.Current }));
        group.MapGet("/furniture", (string? q) => Results.Ok(catalog.SearchFurniture(q)));
        group.MapGet(
            "/furniture/list",
            (string? q, string? type, string? tag, int? page, CancellationToken ct) =>
                FurnitureListAsync(q, type, tag, page, ct)
        );
        group.MapGet("/furniture/logics", () => Results.Ok(logics.LogicTypes));
        group.MapGet("/furniture/draft/{bundle}", DraftAsync);
        group.MapPost("/furniture", CreateDefinitionsAsync);
        group.MapGet("/furniture/{id:int}", DefinitionAsync);
        group.MapPut("/furniture/{id:int}", UpdateDefinitionAsync);
        group.MapGet("/furniture/habbo-values", HabboValuesPreviewAsync);
        group.MapPost("/furniture/habbo-values", TakeHabboValuesAsync);
        group.MapPost("/files/{file}/build", RebuildAsync);
        group.MapPut("/files/{file}/key", SetFileKeyAsync);
        group.MapGet("/texts/import", TextPreviewAsync);
        group.MapPost("/texts/import/{versionId:int}", TextImportAsync);
        group.MapGet(
            "/texts",
            (string? q, int? page, string? plugin, CancellationToken ct) =>
                TextSearchAsync(q, page, plugin, ct)
        );
        group.MapPut("/texts", SaveTextAsync);
        group.MapDelete("/texts", DeleteTextAsync);
        group
            .MapPost(
                "/texts/habbo",
                async (HotelViewTextsRequest request, CancellationToken ct) =>
                    Results.Ok(
                        await hotelView
                            .GetHabboTextsAsync(request.Keys ?? [], ct)
                            .ConfigureAwait(false)
                    )
            )
            .ViewIsEnough("It reads Habbo's texts of the keys named, and changes nothing.");
        group.MapGet(
            "/variables",
            (string? q, int? page, CancellationToken ct) => VariableSearchAsync(q, page, ct)
        );
        group.MapGet(
            "/variables/habbo",
            (string? prefix, CancellationToken ct) => HabboVariablesAsync(prefix, ct)
        );
        group.MapGet("/variables/habbo/update", HabboVariableUpdateAsync);
        group.MapPost("/variables/habbo/update/{versionId:int}", TakeHabboVariablesAsync);
        group.MapPut("/variables", SaveVariableAsync);
        group.MapPut("/variables/link", LinkVariableAsync);
        group.MapDelete("/variables", DeleteVariableAsync);
        group.MapPost("/variables/import/preview", VariablePreviewAsync);
        group.MapPost("/variables/import", VariableImportAsync);
        group.MapGet("/hotel-view", HotelViewAsync);
        group
            .MapPost("/hotel-view/texts", HotelViewTextsAsync)
            .ViewIsEnough("It reads the texts the keys name, and changes nothing.");
        group.MapGet("/hotel-view/habbo", HabboHotelViewAsync);
        group.MapPost(
            "/hotel-view/habbo-texts",
            async (HotelViewTextsRequest request, CancellationToken ct) =>
                Results.Ok(
                    await hotelView.GetHabboTextsAsync(request.Keys ?? [], ct).ConfigureAwait(false)
                )
        );
        group.MapPut("/hotel-view", SaveHotelViewAsync);
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
            .RequireAnyPermission(
                NO_ACCESS,
                PermissionNodes.Admin.CATALOG_VIEW,
                PermissionNodes.Admin.GAMEDATA_VIEW
            )
            .MapGet("/", ProductLookupAsync);
        group.MapGet(
            "/history",
            (int? page, GamedataRecordType[]? type, CancellationToken ct) =>
                HistoryAsync(page, type, ct)
        );
        group.MapGet("/history/{id:int}", ChangesAsync);
        group.MapPost("/history/{id:int}/rollback", RollbackAsync);
    }

    private async Task<IResult> StatusAsync(HttpContext http, CancellationToken ct)
    {
        var latest = await releases.GetLatestAsync(ct).ConfigureAwait(false);
        var latestTexts = await releases.GetLatestTextsAsync(ct).ConfigureAwait(false);
        var latestProducts = await releases.GetLatestProductsAsync(ct).ConfigureAwait(false);
        var latestFigures = await releases.GetLatestFiguresAsync(ct).ConfigureAwait(false);
        var latestVariables = await releases.GetLatestVariablesAsync(ct).ConfigureAwait(false);
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
        var externalVariables = await files
            .GetCurrentAsync(GamedataFiles.EXTERNAL_VARIABLES, ct)
            .ConfigureAwait(false);
        var figureMap = await files
            .GetCurrentAsync(GamedataFiles.FIGURE_MAP, ct)
            .ConfigureAwait(false);
        var effectMap = await files
            .GetCurrentAsync(GamedataFiles.EFFECT_MAP, ct)
            .ConfigureAwait(false);

        return Results.Ok(
            new GamedataStatusResponse(
                latest,
                latestTexts,
                latestProducts,
                latestFigures,
                latestVariables,
                furnitureData.File,
                externalTexts.File,
                productData.File,
                figureData.File,
                externalVariables.File,
                figureMap.File,
                effectMap.File,
                await variables.GetFileKeysAsync(ct).ConfigureAwait(false),
                await grainFactory
                    .HoldsAsync(http, PermissionNodes.Gamedata.MANAGE, ct)
                    .ConfigureAwait(false)
            )
        );
    }

    private async Task<IResult> CheckAsync(HttpContext http, CancellationToken ct)
    {
        try
        {
            var result = await releases.CheckAsync(ct).ConfigureAwait(false);

            assetSync.StartAfterCheck(AdminIdentity.Of(http).PlayerId);

            // The HTML client too: a new release downloads in the background.
            await client.CheckAsync(wait: false, ct).ConfigureAwait(false);

            return Results.Ok(result);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Checking Habbo from the panel failed: {Message}", ex.Message);

            return AdminResults.Error(
                StatusCodes.Status502BadGateway,
                $"Habbo could not be checked: {ex.Message}"
            );
        }
    }

    private async Task<IResult> PreviewAsync(int? releaseId, CancellationToken ct) =>
        await furniture.PreviewImportAsync(releaseId, ct).ConfigureAwait(false) is { } preview
            ? Results.Ok(preview)
            : AdminResults.Error(StatusCodes.Status404NotFound, NO_RELEASE);

    private async Task<IResult> ImportAsync(int releaseId, HttpContext http, CancellationToken ct)
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

    private async Task<IResult> FurnitureListAsync(
        string? q,
        string? type,
        string? tag,
        int? page,
        CancellationToken ct
    )
    {
        ProductType? kind = null;
        FurnitureTag? only = null;

        if (!string.IsNullOrWhiteSpace(type))
        {
            if (!FurnitureTagNames.TryParseType(type, out var parsed))
                return AdminResults.Error(
                    StatusCodes.Status400BadRequest,
                    "The type is floor or wall."
                );

            kind = parsed;
        }

        if (!string.IsNullOrWhiteSpace(tag))
        {
            if (!FurnitureTagNames.TryParse(tag, out var parsed))
                return AdminResults.Error(StatusCodes.Status400BadRequest, $"{tag} is not a tag.");

            only = parsed;
        }

        return Results.Ok(
            FurnitureListResponse.From(
                await furniture.ListAsync(q, kind, only, page ?? 0, ct).ConfigureAwait(false)
            )
        );
    }

    private async Task<IResult> DraftAsync(string bundle, CancellationToken ct)
    {
        try
        {
            return
                await furniture.DraftFromBundleAsync(bundle, ct).ConfigureAwait(false) is { } draft
                ? Results.Ok(FurnitureDraftResponse.From(draft))
                : AdminResults.Error(
                    StatusCodes.Status404NotFound,
                    $"There is no furniture bundle {bundle} on this server."
                );
        }
        catch (ArgumentException ex)
        {
            return AdminResults.Error(StatusCodes.Status422UnprocessableEntity, ex.Message);
        }
    }

    private async Task<IResult> CreateDefinitionsAsync(
        FurnitureDefinitionCreateRequest request,
        HttpContext http,
        CancellationToken ct
    )
    {
        if (!FurnitureTagNames.TryParseType(request.Type ?? string.Empty, out var type))
            return AdminResults.Error(
                StatusCodes.Status400BadRequest,
                "The type is floor or wall."
            );

        try
        {
            var ids = await furniture
                .CreateDefinitionsAsync(
                    type,
                    request.Fields ?? [],
                    request.Colors ?? [],
                    AdminIdentity.Of(http).PlayerId,
                    ct
                )
                .ConfigureAwait(false);
            var definition = await furniture.GetDefinitionAsync(ids[0], ct).ConfigureAwait(false);

            return Results.Ok(
                new FurnitureDefinitionCreateResponse(
                    [.. ids],
                    FurnitureDefinitionResponse.From(definition!)
                )
            );
        }
        catch (ArgumentException ex)
        {
            return AdminResults.Error(StatusCodes.Status400BadRequest, ex.Message);
        }
    }

    private async Task<IResult> DefinitionAsync(int id, CancellationToken ct) =>
        await furniture.GetDefinitionAsync(id, ct).ConfigureAwait(false) is { } definition
            ? Results.Ok(FurnitureDefinitionResponse.From(definition))
            : Results.NotFound();

    private async Task<IResult> UpdateDefinitionAsync(
        int id,
        FurnitureDefinitionUpdateRequest request,
        HttpContext http,
        CancellationToken ct
    )
    {
        if (request.Fields is not { Count: > 0 } fields)
            return AdminResults.Error(StatusCodes.Status400BadRequest, "Nothing to change.");

        try
        {
            return
                await furniture
                    .UpdateDefinitionAsync(id, fields, AdminIdentity.Of(http).PlayerId, ct)
                    .ConfigureAwait(false)
                    is { } definition
                ? Results.Ok(FurnitureDefinitionResponse.From(definition))
                : Results.NotFound();
        }
        catch (ArgumentException ex)
        {
            return AdminResults.Error(StatusCodes.Status400BadRequest, ex.Message);
        }
    }

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

    private async Task<IResult> TakeHabboValuesAsync(
        HabboValuesRequest request,
        HttpContext http,
        CancellationToken ct
    )
    {
        try
        {
            var changeSet = await furniture
                .TakeHabboValuesAsync(request.Fields ?? [], AdminIdentity.Of(http).PlayerId, ct)
                .ConfigureAwait(false);

            return Results.Ok(new { changeSet });
        }
        catch (ArgumentException ex)
        {
            return AdminResults.Error(StatusCodes.Status400BadRequest, ex.Message);
        }
    }

    /// <summary>A comma-separated list of furnidata keys from the query.</summary>
    private static string[] FieldList(string? fields) =>
        (fields ?? string.Empty).Split(
            ',',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
        );

    private async Task<IResult> RebuildAsync(string file, HttpContext http, CancellationToken ct) =>
        GamedataFiles.IsKnown(file)
            ? Results.Ok((await files.RebuildAsync(file, ct).ConfigureAwait(false)).File)
            : Results.NotFound();

    private async Task<IResult> SetFileKeyAsync(
        string file,
        FileKeyRequest request,
        HttpContext http,
        CancellationToken ct
    )
    {
        try
        {
            return Results.Ok(
                await variables
                    .SetFileKeyAsync(
                        file,
                        request.Key ?? string.Empty,
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

    private async Task<IResult> HabboVariableUpdateAsync(int? versionId, CancellationToken ct) =>
        await habboVariables.PreviewAsync(versionId, ct).ConfigureAwait(false) is { } preview
            ? Results.Ok(preview)
            : AdminResults.Error(StatusCodes.Status404NotFound, NO_VARIABLES);

    private async Task<IResult> TakeHabboVariablesAsync(
        int versionId,
        HabboVariableTakeRequest request,
        HttpContext http,
        CancellationToken ct
    )
    {
        try
        {
            return Results.Ok(
                new
                {
                    changeSet = await habboVariables
                        .TakeAsync(versionId, request.Keys, AdminIdentity.Of(http).PlayerId, ct)
                        .ConfigureAwait(false),
                }
            );
        }
        catch (ArgumentException ex)
        {
            return AdminResults.Error(StatusCodes.Status404NotFound, ex.Message);
        }
    }

    private async Task<IResult> TextPreviewAsync(int? versionId, CancellationToken ct) =>
        await texts.PreviewImportAsync(versionId, ct).ConfigureAwait(false) is { } preview
            ? Results.Ok(preview)
            : AdminResults.Error(StatusCodes.Status404NotFound, NO_TEXTS);

    private async Task<IResult> TextImportAsync(
        int versionId,
        HttpContext http,
        CancellationToken ct
    )
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

    private async Task<IResult> ProductPreviewAsync(int? versionId, CancellationToken ct) =>
        await products.PreviewImportAsync(versionId, ct).ConfigureAwait(false) is { } preview
            ? Results.Ok(preview)
            : AdminResults.Error(StatusCodes.Status404NotFound, NO_PRODUCTS);

    private async Task<IResult> ProductImportAsync(
        int versionId,
        HttpContext http,
        CancellationToken ct
    )
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

    private async Task<IResult> SaveProductAsync(
        ProductSaveRequest request,
        HttpContext http,
        CancellationToken ct
    )
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

    private async Task<IResult> DeleteProductAsync(
        string? code,
        HttpContext http,
        CancellationToken ct
    ) =>
        await products
            .DeleteAsync(code ?? string.Empty, AdminIdentity.Of(http).PlayerId, ct)
            .ConfigureAwait(false)
            ? Results.NoContent()
            : Results.NotFound();

    private async Task<IResult> FigurePreviewAsync(int? versionId, CancellationToken ct) =>
        await figures.PreviewImportAsync(versionId, ct).ConfigureAwait(false) is { } preview
            ? Results.Ok(preview)
            : AdminResults.Error(StatusCodes.Status404NotFound, NO_FIGURES);

    private async Task<IResult> FigureImportAsync(
        int versionId,
        HttpContext http,
        CancellationToken ct
    )
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

    private async Task<IResult> SaveFigureAsync(
        FigureSaveRequest request,
        HttpContext http,
        CancellationToken ct
    )
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

    private async Task<IResult> PalettesAsync(CancellationToken ct) =>
        Results.Ok(await figures.GetPalettesAsync(ct).ConfigureAwait(false));

    private async Task<IResult> SaveFigureBatchAsync(
        FigureBatchRequest request,
        HttpContext http,
        CancellationToken ct
    )
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

    private async Task<IResult> DeleteFigureAsync(
        FigureRecordKind kind,
        string? key,
        HttpContext http,
        CancellationToken ct
    ) =>
        await figures
            .DeleteAsync(kind, key ?? string.Empty, AdminIdentity.Of(http).PlayerId, ct)
            .ConfigureAwait(false)
            ? Results.NoContent()
            : Results.NotFound();

    private async Task<IResult> OwnedClothingAsync(int playerId, CancellationToken ct) =>
        Results.Ok(
            new
            {
                setIds = (await clothing.GetOwnedAsync(playerId, ct).ConfigureAwait(false))
                    .Order()
                    .ToArray(),
            }
        );

    private async Task<IResult> GrantClothingAsync(
        int playerId,
        ClothingGrantRequest request,
        HttpContext http,
        CancellationToken ct
    )
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

    private async Task<IResult> RevokeClothingAsync(
        int playerId,
        ClothingGrantRequest request,
        HttpContext http,
        CancellationToken ct
    )
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

    private async Task<IResult> TextSearchAsync(
        string? q,
        int? page,
        string? plugin,
        CancellationToken ct
    ) => Results.Ok(await texts.SearchAsync(q, page ?? 0, ct, plugin).ConfigureAwait(false));

    private async Task<IResult> SaveTextAsync(
        TextSaveRequest request,
        HttpContext http,
        CancellationToken ct
    )
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

    private async Task<IResult> DeleteTextAsync(
        string? key,
        HttpContext http,
        CancellationToken ct
    ) =>
        await texts
            .DeleteAsync(key ?? string.Empty, AdminIdentity.Of(http).PlayerId, ct)
            .ConfigureAwait(false)
            ? Results.NoContent()
            : Results.NotFound();

    private async Task<IResult> VariableSearchAsync(string? q, int? page, CancellationToken ct) =>
        Results.Ok(await variables.SearchAsync(q, page ?? 0, ct).ConfigureAwait(false));

    /// <summary>Habbo's variables under a prefix now, beside the hotel's; 502 when Habbo can't be read.</summary>
    private async Task<IResult> HabboVariablesAsync(string? prefix, CancellationToken ct)
    {
        try
        {
            return Results.Ok(
                await variables.PreviewHabboAsync(prefix ?? string.Empty, ct).ConfigureAwait(false)
            );
        }
        catch (ArgumentException ex)
        {
            return AdminResults.Error(StatusCodes.Status400BadRequest, ex.Message);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Habbo's variables could not be read for the panel");

            return AdminResults.Error(
                StatusCodes.Status502BadGateway,
                $"Habbo could not be read: {ex.Message}"
            );
        }
    }

    private async Task<IResult> SaveVariableAsync(
        VariableSaveRequest request,
        HttpContext http,
        CancellationToken ct
    )
    {
        try
        {
            return Results.Ok(
                await variables
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

    private async Task<IResult> LinkVariableAsync(
        VariableLinkRequest request,
        HttpContext http,
        CancellationToken ct
    )
    {
        try
        {
            return Results.Ok(
                await variables
                    .LinkAsync(
                        request.Key ?? string.Empty,
                        request.Setting,
                        request.File,
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

    private async Task<IResult> DeleteVariableAsync(
        string? key,
        HttpContext http,
        CancellationToken ct
    ) =>
        await variables
            .DeleteAsync(key ?? string.Empty, AdminIdentity.Of(http).PlayerId, ct)
            .ConfigureAwait(false)
            ? Results.NoContent()
            : Results.NotFound();

    private async Task<IResult> VariablePreviewAsync(
        VariableImportRequest request,
        HttpContext http,
        CancellationToken ct
    )
    {
        try
        {
            return Results.Ok(
                await variables
                    .PreviewImportAsync(request.Json ?? string.Empty, request.RemoveMissing, ct)
                    .ConfigureAwait(false)
            );
        }
        catch (ArgumentException ex)
        {
            return AdminResults.Error(StatusCodes.Status400BadRequest, ex.Message);
        }
    }

    private async Task<IResult> VariableImportAsync(
        VariableImportRequest request,
        HttpContext http,
        CancellationToken ct
    )
    {
        try
        {
            return Results.Ok(
                new
                {
                    changeSet = await variables
                        .ImportAsync(
                            request.Json ?? string.Empty,
                            request.RemoveMissing,
                            AdminIdentity.Of(http).PlayerId,
                            ct
                        )
                        .ConfigureAwait(false),
                }
            );
        }
        catch (ArgumentException ex)
        {
            return AdminResults.Error(StatusCodes.Status400BadRequest, ex.Message);
        }
    }

    private async Task<IResult> HotelViewAsync(HttpContext http, CancellationToken ct) =>
        Results.Ok(
            new
            {
                variables = await hotelView.GetVariablesAsync(ct).ConfigureAwait(false),
                canManage = await grainFactory
                    .HoldsAsync(http, PermissionNodes.Gamedata.MANAGE, ct)
                    .ConfigureAwait(false),
            }
        );

    private async Task<IResult> HotelViewTextsAsync(
        HotelViewTextsRequest request,
        CancellationToken ct
    ) =>
        Results.Ok(
            new
            {
                texts = await hotelView.GetTextsAsync(request.Keys ?? [], ct).ConfigureAwait(false),
            }
        );

    /// <summary>Habbo's reception now, beside the hotel's; 502 when Habbo can't be read.</summary>
    private async Task<IResult> HabboHotelViewAsync(CancellationToken ct)
    {
        try
        {
            return Results.Ok(await hotelView.PreviewHabboAsync(ct).ConfigureAwait(false));
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Habbo's reception could not be read for the panel");

            return AdminResults.Error(
                StatusCodes.Status502BadGateway,
                $"Habbo could not be read: {ex.Message}"
            );
        }
    }

    private async Task<IResult> SaveHotelViewAsync(
        HotelViewSaveRequest request,
        HttpContext http,
        CancellationToken ct
    )
    {
        try
        {
            return Results.Ok(
                new
                {
                    changeSet = await hotelView
                        .SaveAsync(
                            new HotelViewEdit
                            {
                                Variables = request.Variables ?? [],
                                Texts = request.Texts ?? [],
                            },
                            AdminIdentity.Of(http).PlayerId,
                            ct
                        )
                        .ConfigureAwait(false),
                }
            );
        }
        catch (ArgumentException ex)
        {
            return AdminResults.Error(StatusCodes.Status400BadRequest, ex.Message);
        }
    }

    private async Task<IResult> HistoryAsync(
        int? page,
        GamedataRecordType[]? types,
        CancellationToken ct
    ) => Results.Ok(await history.PageAsync(page ?? 0, types ?? [], ct).ConfigureAwait(false));

    private async Task<IResult> ChangesAsync(int id, CancellationToken ct) =>
        await history.GetChangesAsync(id, ct).ConfigureAwait(false) is { } changes
            ? Results.Ok(changes)
            : Results.NotFound();

    private async Task<IResult> RollbackAsync(int id, HttpContext http, CancellationToken ct)
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
}
