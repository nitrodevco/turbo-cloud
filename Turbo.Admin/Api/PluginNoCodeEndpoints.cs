using System;
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
    IPluginStyles styles,
    IHotelDataPlugin hotel,
    AdminActionLog audit
)
{
    private const string NO_ACCESS = "You can't see the server's plugins.";
    private const string NO_MANAGE = "You can't change the server's plugins.";
    private const string NO_PLUGIN = "There is no plugin with that key.";
    private const string NO_STYLE = "The theme has no such style.";
    private const long IMAGE_MAX_BYTES = 2 * 1024 * 1024;

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
        group.MapPost("/{key}/package", PackageAsync);
    }

    private async Task<IResult> StylesAsync(string key, HttpContext http, CancellationToken ct)
    {
        if (hotel.FolderOf(key) is not { } folder)
            return AdminResults.Error(StatusCodes.Status404NotFound, NO_PLUGIN);

        try
        {
            var list = await styles.ListAsync(key, folder, ct).ConfigureAwait(false);

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
                hotel.FolderOf(key) is { } folder
                    ? Results.Ok(
                        PluginStyleView.From(
                            await styles
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
                hotel.FolderOf(key) is not { } folder
                    ? AdminResults.Error(StatusCodes.Status404NotFound, NO_PLUGIN)
                : await styles.ResetAsync(key, folder, name, player, ct).ConfigureAwait(false)
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
                if (hotel.FolderOf(key) is not { } folder)
                    return AdminResults.Error(StatusCodes.Status404NotFound, NO_PLUGIN);

                if (
                    http.Features.Get<IHttpMaxRequestBodySizeFeature>() is
                    { IsReadOnly: false } limit
                )
                    limit.MaxRequestBodySize = IMAGE_MAX_BYTES + 64 * 1024;

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

                var url = await hotel
                    .SaveFileAsync(
                        $"styles/{key}/{safe}-{hash}{Path.GetExtension(file.FileName).ToLowerInvariant()}",
                        memory,
                        ct
                    )
                    .ConfigureAwait(false);

                return Results.Ok(
                    PluginStyleView.From(
                        await styles
                            .SetAsync(key, folder, name, url, player, ct)
                            .ConfigureAwait(false)
                    )
                );
            }
        );

    private async Task<IResult> PatchesAsync(HttpContext http, CancellationToken ct) =>
        Results.Ok(
            new HotelPatchesResponse(
                IHotelDataPlugin.KEY,
                await hotel.PatchesAsync(ct).ConfigureAwait(false),
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
            async _ => Results.Ok(await hotel.SavePatchAsync(patch, ct).ConfigureAwait(false))
        );

    private Task<IResult> RemovePatchAsync(string id, HttpContext http, CancellationToken ct) =>
        ManageAsync(
            http,
            $"patch {id} removed",
            ct,
            async _ =>
                await hotel.RemovePatchAsync(id, ct).ConfigureAwait(false)
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
                await hotel.ExportAsync(key, request, ct).ConfigureAwait(false) is { } zip
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
    double? Min,
    double? Max,
    string Default,
    string Value,
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

/// <summary>The patches staff made, and the key of the plugin that holds them.</summary>
internal sealed record HotelPatchesResponse(
    string Key,
    ImmutableArray<HotelPatch> Patches,
    bool CanManage
);
