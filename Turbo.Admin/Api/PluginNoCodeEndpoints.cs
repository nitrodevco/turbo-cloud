using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Orleans;
using Turbo.Admin.Commands;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Plugins;

namespace Turbo.Admin.Api;

/// <summary>
/// The no-code side of plugins (<c>docs/plugin-data.md</c>), for staff with
/// <c>admin.plugins.view</c>: a theme's style properties with the hotel's values, the window
/// patches staff made (the hotel's own data plugin), and a data plugin made from either. Changing
/// a style or a patch, uploading an image and making a package need <c>admin.plugins.manage</c>;
/// each change goes to the command log (<see cref="AdminActionLog"/>).
/// </summary>
internal sealed class PluginNoCodeEndpoints(
    IGrainFactory grainFactory,
    AdminActionLog audit,
    IPluginStyles? stylesService = null,
    IHotelDataPlugin? hotelService = null
)
{
    private const string NO_ACCESS = "You can't see the server's plugins.";
    private const string NO_MANAGE = "You can't change the server's plugins.";
    private const string NO_PLUGIN = "There is no plugin with that key.";
    private const string NO_STYLE = "The theme has no such style.";
    private const long IMAGE_MAX_BYTES = 2 * 1024 * 1024;

    // A host without plugins (an admin API on its own) has neither: every route answers 404.
    private IPluginStyles Styles =>
        stylesService ?? throw new KeyNotFoundException("The server runs no plugins.");

    private IHotelDataPlugin Hotel =>
        hotelService ?? throw new KeyNotFoundException("The server runs no plugins.");

    public void Map(RouteGroupBuilder secured)
    {
        var group = secured
            .MapGroup("/plugins")
            .RequirePermission(PermissionNodes.Admin.PLUGINS_VIEW, NO_ACCESS)
            .ChecksInHandler(
                "Each change checks admin.plugins.manage in the handler, so a refusal is written to the command log too."
            );

        group.MapGet("/hotel-patches", PatchesAsync);
        group.MapPost("/hotel-patches", SavePatchAsync);
        group.MapDelete("/hotel-patches/{id}", RemovePatchAsync);
        group.MapGet("/{key}/styles", StylesAsync);
        group.MapPut("/{key}/styles/{name}", SetStyleAsync);
        group.MapDelete("/{key}/styles/{name}", ResetStyleAsync);
        group.MapPost("/{key}/styles/{name}/image", UploadStyleImageAsync);
        group.MapGet("/{key}/images", ImagesAsync);
        group.MapPost("/{key}/images", UploadImageAsync);
        group.MapDelete("/{key}/images", RemoveImageAsync);
        group.MapPost("/{key}/package", PackageAsync);
    }

    private async Task<IResult> StylesAsync(string key, HttpContext http, CancellationToken ct)
    {
        if (stylesService is null || hotelService?.FolderOf(key) is not { } folder)
            return AdminResults.Error(StatusCodes.Status404NotFound, NO_PLUGIN);

        try
        {
            var list = await Styles.ListAsync(key, folder, ct).ConfigureAwait(false);

            return Results.Ok(
                new PluginStylesResponse(
                    key,
                    [.. list.Select(PluginStyleView.From)],
                    await HoldsAsync(http, PermissionNodes.Admin.PLUGINS_MANAGE, ct)
                        .ConfigureAwait(false)
                )
            );
        }
        catch (ArgumentException ex)
        {
            return AdminResults.Error(StatusCodes.Status422UnprocessableEntity, ex.Message);
        }
    }

    private Task<IResult> SetStyleAsync(
        string key,
        string name,
        PluginStyleInput input,
        HttpContext http,
        CancellationToken ct
    ) =>
        ManageAsync(
            http,
            $"style {key} {name}={input.Value}",
            ct,
            async player =>
                Hotel.FolderOf(key) is { } folder
                    ? Results.Ok(
                        PluginStyleView.From(
                            await Styles
                                .SetAsync(
                                    key,
                                    folder,
                                    name,
                                    input.Value ?? string.Empty,
                                    player,
                                    ct
                                )
                                .ConfigureAwait(false)
                        )
                    )
                    : AdminResults.Error(StatusCodes.Status404NotFound, NO_PLUGIN)
        );

    private Task<IResult> ResetStyleAsync(
        string key,
        string name,
        HttpContext http,
        CancellationToken ct
    ) =>
        ManageAsync(
            http,
            $"style {key} {name} reset",
            ct,
            async player =>
                Hotel.FolderOf(key) is not { } folder
                    ? AdminResults.Error(StatusCodes.Status404NotFound, NO_PLUGIN)
                : await Styles.ResetAsync(key, folder, name, player, ct).ConfigureAwait(false)
                    is { } style
                    ? Results.Ok(PluginStyleView.From(style))
                : AdminResults.Error(StatusCodes.Status404NotFound, NO_STYLE)
        );

    /// <summary>
    /// An image for an image style: kept in the hotel's own plugin (so it stays when the theme is
    /// updated), named by its content, and given to the style as its address.
    /// </summary>
    private Task<IResult> UploadStyleImageAsync(
        string key,
        string name,
        HttpContext http,
        CancellationToken ct
    ) =>
        ManageAsync(
            http,
            $"style {key} {name} image",
            ct,
            async player =>
            {
                if (Hotel.FolderOf(key) is not { } folder)
                    return AdminResults.Error(StatusCodes.Status404NotFound, NO_PLUGIN);

                if (ImageLimit(http) is { } refused)
                    return refused;

                var form = await http.Request.ReadFormAsync(ct).ConfigureAwait(false);

                if (form.Files.GetFile("file") is not { } file)
                    return AdminResults.Error(StatusCodes.Status400BadRequest, "Choose the image.");

                if (file.Length > IMAGE_MAX_BYTES)
                    return AdminResults.Error(
                        StatusCodes.Status413PayloadTooLarge,
                        "An image is at most 2 MB."
                    );

                using var memory = new MemoryStream((int)file.Length);
                var stream = file.OpenReadStream();
                await using (stream.ConfigureAwait(false))
                    await stream.CopyToAsync(memory, ct).ConfigureAwait(false);

                var hash = Convert.ToHexStringLower(SHA256.HashData(memory.ToArray()))[..12];
                var safe = new string([.. name.Select(c => char.IsLetterOrDigit(c) ? c : '-')]);

                memory.Position = 0;

                // Kept in the hotel's own plugin, so it stays when the theme is updated.
                var image = await Hotel
                    .SaveImageAsync(
                        IHotelDataPlugin.KEY,
                        $"{key}-{safe}-{hash}{Path.GetExtension(file.FileName).ToLowerInvariant()}",
                        memory,
                        ct
                    )
                    .ConfigureAwait(false);
                var url = image.Url ?? $"/gamedata/plugins/{IHotelDataPlugin.KEY}/{image.Path}";

                return Results.Ok(
                    PluginStyleView.From(
                        await Styles
                            .SetAsync(key, folder, name, url, player, ct)
                            .ConfigureAwait(false)
                    )
                );
            }
        );

    private async Task<IResult> ImagesAsync(string key, HttpContext http, CancellationToken ct) =>
        hotelService is null
        || await Hotel.ImagesAsync(key, ct).ConfigureAwait(false) is not { } images
            ? AdminResults.Error(StatusCodes.Status404NotFound, NO_PLUGIN)
            : Results.Ok(
                new PluginImagesResponse(
                    key,
                    images,
                    await HoldsAsync(http, PermissionNodes.Admin.PLUGINS_MANAGE, ct)
                        .ConfigureAwait(false)
                )
            );

    /// <summary>
    /// An image for the plugin's <c>wwwroot/styles/</c>, under its own file name (or the form's
    /// <c>name</c>), so its patches name it by its path and its styles and variables by its address.
    /// </summary>
    private Task<IResult> UploadImageAsync(string key, HttpContext http, CancellationToken ct) =>
        ManageAsync(
            http,
            $"image {key} upload",
            ct,
            async _ =>
            {
                if (ImageLimit(http) is { } refused)
                    return refused;

                var form = await http.Request.ReadFormAsync(ct).ConfigureAwait(false);

                if (form.Files.GetFile("file") is not { } file)
                    return AdminResults.Error(StatusCodes.Status400BadRequest, "Choose the image.");

                if (file.Length > IMAGE_MAX_BYTES)
                    return AdminResults.Error(
                        StatusCodes.Status413PayloadTooLarge,
                        "An image is at most 2 MB."
                    );

                var name = form["name"].ToString() is { Length: > 0 } given ? given : file.FileName;
                var stream = file.OpenReadStream();

                await using (stream.ConfigureAwait(false))
                    return Results.Ok(
                        await Hotel.SaveImageAsync(key, name, stream, ct).ConfigureAwait(false)
                    );
            }
        );

    private Task<IResult> RemoveImageAsync(
        string key,
        string path,
        HttpContext http,
        CancellationToken ct
    ) =>
        ManageAsync(
            http,
            $"image {key} {path} removed",
            ct,
            async _ =>
                await Hotel.RemoveImageAsync(key, path, ct).ConfigureAwait(false)
                    ? Results.NoContent()
                    : AdminResults.Error(StatusCodes.Status404NotFound, "There is no such image.")
        );

    /// <summary>Lets the request body be an image's size; null, or the refusal of a body that says it is bigger.</summary>
    private static IResult? ImageLimit(HttpContext http)
    {
        if (http.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
            limit.MaxRequestBodySize = IMAGE_MAX_BYTES + 64 * 1024;

        return http.Request.ContentLength > IMAGE_MAX_BYTES + 64 * 1024
            ? AdminResults.Error(StatusCodes.Status413PayloadTooLarge, "An image is at most 2 MB.")
            : null;
    }

    private async Task<IResult> PatchesAsync(HttpContext http, CancellationToken ct) =>
        hotelService is null
            ? AdminResults.Error(StatusCodes.Status404NotFound, "The server runs no plugins.")
            : Results.Ok(
                new HotelPatchesResponse(
                    IHotelDataPlugin.KEY,
                    await Hotel.PatchesAsync(ct).ConfigureAwait(false),
                    await HoldsAsync(http, PermissionNodes.Admin.PLUGINS_MANAGE, ct)
                        .ConfigureAwait(false)
                )
            );

    private Task<IResult> SavePatchAsync(
        HotelPatch patch,
        HttpContext http,
        CancellationToken ct
    ) =>
        ManageAsync(
            http,
            $"patch {patch.Template} {patch.Name}{(patch.Disabled ? " (disabled)" : "")}",
            ct,
            async _ => Results.Ok(await Hotel.SavePatchAsync(patch, ct).ConfigureAwait(false))
        );

    private Task<IResult> RemovePatchAsync(string id, HttpContext http, CancellationToken ct) =>
        ManageAsync(
            http,
            $"patch {id} removed",
            ct,
            async _ =>
                await Hotel.RemovePatchAsync(id, ct).ConfigureAwait(false)
                    ? Results.NoContent()
                    : AdminResults.Error(StatusCodes.Status404NotFound, "There is no such patch.")
        );

    private Task<IResult> PackageAsync(
        string key,
        PluginExportRequest request,
        HttpContext http,
        CancellationToken ct
    ) =>
        ManageAsync(
            http,
            $"package {key} as {request.Key} {request.Version}",
            ct,
            async _ =>
                await Hotel.ExportAsync(key, request, ct).ConfigureAwait(false) is { } zip
                    ? Results.File(zip, "application/zip", $"{request.Key}-{request.Version}.zip")
                    : AdminResults.Error(StatusCodes.Status404NotFound, NO_PLUGIN)
        );

    /// <summary>
    /// A change by someone who holds <c>admin.plugins.manage</c>, written to the command log with
    /// how it went; a value refused is a 400 with why.
    /// </summary>
    private async Task<IResult> ManageAsync(
        HttpContext http,
        string arguments,
        CancellationToken ct,
        Func<Turbo.Primitives.Players.PlayerId, Task<IResult>> action
    )
    {
        var player = AdminIdentity.Of(http).PlayerId;

        if (!await HoldsAsync(http, PermissionNodes.Admin.PLUGINS_MANAGE, ct).ConfigureAwait(false))
        {
            await audit
                .WriteAsync(AdminActionLog.PLUGIN, player, arguments, CommandOutcome.Refused, ct)
                .ConfigureAwait(false);

            return AdminResults.Error(StatusCodes.Status403Forbidden, NO_MANAGE);
        }

        IResult result;
        var outcome = CommandOutcome.Completed;

        try
        {
            result = await action(player).ConfigureAwait(false);
        }
        catch (ArgumentException ex)
        {
            outcome = CommandOutcome.Failed;
            result = AdminResults.Error(StatusCodes.Status400BadRequest, ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            outcome = CommandOutcome.Failed;
            result = AdminResults.Error(StatusCodes.Status404NotFound, ex.Message);
        }

        await audit
            .WriteAsync(AdminActionLog.PLUGIN, player, arguments, outcome, CancellationToken.None)
            .ConfigureAwait(false);

        return result;
    }

    private Task<bool> HoldsAsync(HttpContext http, string node, CancellationToken ct) =>
        AdminIdentity.Of(http).HoldsAsync(grainFactory, node, ct);
}

/// <summary>A style value as staff type it: a colour <c>#rrggbb</c>, a number, an image's url, a font's name.</summary>
internal sealed record PluginStyleInput(string? Value);

/// <summary>One style property and the hotel's value of it.</summary>
internal sealed record PluginStyleView(
    string Name,
    string Type,
    string? Group,
    string? Label,
    string? Hint,
    string? Overlay,
    ImmutableArray<string> Variable,
    double? Min,
    double? Max,
    string? Default,
    string? Value,
    bool Changed
)
{
    public static PluginStyleView From(PluginStyle style) =>
        new(
            style.Property.Name,
            JsonNamingPolicy.CamelCase.ConvertName(style.Property.Type.ToString()),
            style.Property.Group,
            style.Property.Label,
            style.Property.Hint,
            style.Property.Overlay,
            style.Property.Variable.IsDefault ? [] : style.Property.Variable,
            style.Property.Min,
            style.Property.Max,
            style.Property.Default,
            style.Value,
            style.Changed
        );
}

/// <summary>A theme's style properties; <c>canManage</c>: may change them.</summary>
internal sealed record PluginStylesResponse(
    string Key,
    ImmutableArray<PluginStyleView> Styles,
    bool CanManage
);

/// <summary>The images in a plugin's <c>wwwroot/styles/</c>; <c>canManage</c>: may upload and remove them.</summary>
internal sealed record PluginImagesResponse(
    string Key,
    ImmutableArray<PluginImage> Images,
    bool CanManage
);

/// <summary>The patches staff made, and the key of the plugin that holds them.</summary>
internal sealed record HotelPatchesResponse(
    string Key,
    ImmutableArray<HotelPatch> Patches,
    bool CanManage
);
