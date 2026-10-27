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
using Turbo.Admin.Api.Contracts;
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
/// each change goes to the command log (<see cref="AdminActionLog"/>). Staff's edits land in the
/// plugin the route names (the panel's "Working on"): the hotel's own, or a data theme, whose files
/// they are then written into (<see cref="IHotelDataPlugin.EditableFolderOf"/>); a style's value goes
/// to the hotel unless <c>?target=</c> names the theme itself.
/// </summary>
internal sealed class PluginNoCodeEndpoints(
    IGrainFactory grainFactory,
    AdminActionLog audit,
    IPluginStyles? stylesService = null,
    IHotelDataPlugin? hotelService = null,
    IPluginAdministration? plugins = null,
    IThemeAuthoring? authoring = null
)
{
    private const string NO_ACCESS = "You can't see the server's plugins.";
    private const string NO_MANAGE = "You can't change the server's plugins.";
    private const string NO_PLUGIN = "There is no plugin with that key.";
    private const string NO_STYLE = "The theme has no such style.";
    private const long IMAGE_MAX_BYTES = 2 * 1024 * 1024;

    // The base theme Turbo ships, Habbo's own look: the root every theme tree grows from.
    private const string BASE_THEME = "habbo";

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

        group.MapGet("/themes", ThemesAsync);
        group.MapPost("/themes", CreateThemeAsync);
        group.MapGet("/{key}/template-patches", PluginPatchesAsync);
        group.MapGet("/{key}/patches", PatchesAsync);
        group.MapPost("/{key}/patches", SavePatchAsync);
        group.MapDelete("/{key}/patches/{id}", RemovePatchAsync);
        group.MapGet("/{key}/styles", StylesAsync);
        group.MapPut("/{key}/styles/{name}", SetStyleAsync);
        group.MapDelete("/{key}/styles/{name}", ResetStyleAsync);
        group.MapPost("/{key}/styles/{name}/image", UploadStyleImageAsync);
        group.MapGet("/{key}/images", ImagesAsync);
        group.MapPost("/{key}/images", UploadImageAsync);
        group.MapDelete("/{key}/images", RemoveImageAsync);
        group.MapGet("/{key}/fonts", FontsAsync);
        group.MapPost("/{key}/fonts", UploadFontAsync);
        group.MapDelete("/{key}/fonts", RemoveFontAsync);
        group.MapPut("/{key}/overlays", SetOverlaysAsync);
        group.MapPut("/{key}/texts", SetTextsAsync);
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
                    Styles.ThemeOf(key, folder)?.Extends,
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

    /// <summary>
    /// Every plugin that is a theme (the hotel's own always: it extends the base theme), with the
    /// theme it extends and how many of its style properties the hotel changed, so the panel draws
    /// them as a tree under <see cref="BASE_THEME"/>.
    /// </summary>
    private async Task<IResult> ThemesAsync(HttpContext http, CancellationToken ct)
    {
        if (stylesService is null || hotelService is null)
            return AdminResults.Error(StatusCodes.Status404NotFound, "The server runs no plugins.");

        var found = (plugins?.List() ?? []).ToDictionary(x => x.Key, StringComparer.Ordinal);
        var keys = found.Keys.Append(IHotelDataPlugin.KEY).Distinct(StringComparer.Ordinal);
        var themes = new List<PluginThemeView>();

        foreach (var key in keys.Order(StringComparer.OrdinalIgnoreCase))
        {
            if (Hotel.FolderOf(key) is not { } folder)
                continue;

            var plugin = found.GetValueOrDefault(key);
            var state = plugin is null ? "loaded" : PluginView.StateName(plugin.State);

            try
            {
                if (Styles.ThemeOf(key, folder) is not { } theme)
                    continue;

                var styles = await Styles.ListAsync(key, folder, ct).ConfigureAwait(false);

                themes.Add(
                    new(
                        key,
                        plugin?.Name,
                        state,
                        theme.Extends,
                        styles.Length,
                        styles.Count(x => x.Changed),
                        styles.Count(x => x.Property.From == key),
                        null
                    )
                );
            }
            catch (ArgumentException ex)
            {
                themes.Add(new(key, plugin?.Name, state, null, 0, 0, 0, ex.Message));
            }
        }

        return Results.Ok(
            new PluginThemesResponse(
                BASE_THEME,
                [.. themes],
                await HoldsAsync(http, PermissionNodes.Admin.PLUGINS_MANAGE, ct)
                    .ConfigureAwait(false)
            )
        );
    }

    private async Task<IResult> PluginPatchesAsync(string key, CancellationToken ct)
    {
        if (hotelService is null)
            return AdminResults.Error(StatusCodes.Status404NotFound, "The server runs no plugins.");

        try
        {
            return await Hotel.TemplatePatchesAsync(key, ct).ConfigureAwait(false) is { } patches
                ? Results.Ok(new PluginTemplatePatchesResponse(key, patches))
                : AdminResults.Error(StatusCodes.Status404NotFound, NO_PLUGIN);
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
        string? target,
        HttpContext http,
        CancellationToken ct
    ) =>
        ManageAsync(
            http,
            $"style {key} {name}={input.Value}{Into(key, target)}",
            ct,
            async player =>
                IntoTheme(key, target)
                    ? Results.Ok(
                        PluginStyleView.From(
                            await SetDefaultAsync(key, name, input.Value ?? string.Empty, ct)
                                .ConfigureAwait(false)
                        )
                    )
                : Hotel.FolderOf(key) is { } folder
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
        string? target,
        HttpContext http,
        CancellationToken ct
    ) =>
        ManageAsync(
            http,
            $"style {key} {name} reset{Into(key, target)}",
            ct,
            async player =>
                IntoTheme(key, target)
                    ? Results.Ok(
                        PluginStyleView.From(
                            await SetDefaultAsync(key, name, null, ct).ConfigureAwait(false)
                        )
                    )
                : Hotel.FolderOf(key) is not { } folder
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
        string? target,
        HttpContext http,
        CancellationToken ct
    ) =>
        ManageAsync(
            http,
            $"style {key} {name} image{Into(key, target)}",
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

                // Into the theme staff work on: its own file, named by its path in its theme.json.
                if (IntoTheme(key, target))
                {
                    var own = await Hotel
                        .SaveImageAsync(
                            key,
                            $"{safe}-{hash}{Path.GetExtension(file.FileName).ToLowerInvariant()}",
                            memory,
                            ct
                        )
                        .ConfigureAwait(false);

                    return Results.Ok(
                        PluginStyleView.From(
                            await SetDefaultAsync(key, name, own.Path, ct).ConfigureAwait(false)
                        )
                    );
                }

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

    private async Task<IResult> FontsAsync(string key, HttpContext http, CancellationToken ct) =>
        hotelService is null
        || await Hotel.FontsAsync(key, ct).ConfigureAwait(false) is not { } fonts
            ? AdminResults.Error(StatusCodes.Status404NotFound, NO_PLUGIN)
            : Results.Ok(
                new PluginFontsResponse(
                    key,
                    fonts,
                    await HoldsAsync(http, PermissionNodes.Admin.PLUGINS_MANAGE, ct)
                        .ConfigureAwait(false)
                )
            );

    /// <summary>
    /// A font file for the plugin's <c>wwwroot/styles/</c>, under its own file name (or the form's
    /// <c>name</c>), given to clients as the family its name says (the hotel's <c>text.fonts</c>), so a
    /// font style property or a text style names it.
    /// </summary>
    private Task<IResult> UploadFontAsync(string key, HttpContext http, CancellationToken ct) =>
        ManageAsync(
            http,
            $"font {key} upload",
            ct,
            async player =>
            {
                if (ImageLimit(http) is { } refused)
                    return refused;

                var form = await http.Request.ReadFormAsync(ct).ConfigureAwait(false);

                if (form.Files.GetFile("file") is not { } file)
                    return AdminResults.Error(StatusCodes.Status400BadRequest, "Choose the font.");

                if (file.Length > IMAGE_MAX_BYTES)
                    return AdminResults.Error(
                        StatusCodes.Status413PayloadTooLarge,
                        "A font is at most 2 MB."
                    );

                var name = form["name"].ToString() is { Length: > 0 } given ? given : file.FileName;
                var stream = file.OpenReadStream();
                PluginFont font;

                await using (stream.ConfigureAwait(false))
                    font = await Hotel.SaveFontAsync(key, name, stream, ct).ConfigureAwait(false);

                await Styles
                    .SetFontAsync(
                        key,
                        Hotel.EditableFolderOf(key),
                        font.Family,
                        key == IHotelDataPlugin.KEY
                            ? font.Url ?? $"/gamedata/plugins/{key}/{font.Path}"
                            : font.Path,
                        player,
                        ct
                    )
                    .ConfigureAwait(false);
                await ReloadThemeAsync(key, ct).ConfigureAwait(false);

                return Results.Ok(font);
            }
        );

    private Task<IResult> RemoveFontAsync(
        string key,
        string path,
        HttpContext http,
        CancellationToken ct
    ) =>
        ManageAsync(
            http,
            $"font {key} {path} removed",
            ct,
            async player =>
            {
                if (!await Hotel.RemoveFontAsync(key, path, ct).ConfigureAwait(false))
                    return AdminResults.Error(
                        StatusCodes.Status404NotFound,
                        "There is no such font."
                    );

                await Styles
                    .SetFontAsync(
                        key,
                        Hotel.EditableFolderOf(key),
                        Path.GetFileNameWithoutExtension(path),
                        null,
                        player,
                        ct
                    )
                    .ConfigureAwait(false);
                await ReloadThemeAsync(key, ct).ConfigureAwait(false);

                return Results.NoContent();
            }
        );

    /// <summary>
    /// A skin set: many library bitmaps replaced (or given back) for the hotel in one change, each
    /// checked before any is written (<see cref="IPluginStyles.SetOverlaysAsync"/>).
    /// </summary>
    private Task<IResult> SetOverlaysAsync(
        string key,
        HotelOverlaysInput input,
        HttpContext http,
        CancellationToken ct
    ) =>
        ManageAsync(
            http,
            $"overlays {key} {input.Overlays?.Count ?? 0} bitmap(s)",
            ct,
            async player =>
            {
                var overlays = await Styles
                    .SetOverlaysAsync(
                        key,
                        Hotel.EditableFolderOf(key),
                        input.Overlays ?? new Dictionary<string, string?>(),
                        player,
                        ct
                    )
                    .ConfigureAwait(false);

                await ReloadThemeAsync(key, ct).ConfigureAwait(false);

                return Results.Ok(new HotelOverlaysResponse(overlays));
            }
        );

    /// <summary>
    /// Texts in a theme's own files (<see cref="IPluginStyles.SetTextsAsync"/>): drawn over the
    /// hotel's for its players, for every key staff never changed; the theme is loaded again.
    /// </summary>
    private Task<IResult> SetTextsAsync(
        string key,
        ThemeTextsInput input,
        HttpContext http,
        CancellationToken ct
    ) =>
        ManageAsync(
            http,
            $"texts {key} {input.Texts?.Count ?? 0} text(s)",
            ct,
            async _ =>
            {
                var texts = await Styles
                    .SetTextsAsync(
                        key,
                        Hotel.EditableFolderOf(key),
                        input.Texts ?? new Dictionary<string, string?>(),
                        ct
                    )
                    .ConfigureAwait(false);

                await ReloadThemeAsync(key, ct).ConfigureAwait(false);

                return Results.Ok(new ThemeTextsResponse(texts));
            }
        );

    /// <summary>
    /// A new theme, made in the plugin folder and loaded (<see cref="IThemeAuthoring.CreateAsync"/>):
    /// one that extends another, or a copy of one.
    /// </summary>
    private Task<IResult> CreateThemeAsync(
        ThemeCreateRequest request,
        HttpContext http,
        CancellationToken ct
    ) =>
        ManageAsync(
            http,
            $"theme {request.Key} made{(request.CopyOf is { Length: > 0 } copy ? $" as a copy of {copy}" : $" extending {request.Extends ?? BASE_THEME}")}",
            ct,
            async _ =>
                authoring is null
                    ? AdminResults.Error(
                        StatusCodes.Status404NotFound,
                        "The server runs no plugins."
                    )
                    : Results.Ok(await authoring.CreateAsync(request, ct).ConfigureAwait(false))
        );

    /// <summary>
    /// Whether a style change goes into the theme's own files (<paramref name="target"/> names the
    /// theme itself) rather than the hotel's values. A target that is neither is refused.
    /// </summary>
    private static bool IntoTheme(string key, string? target) =>
        string.IsNullOrEmpty(target) || target == IHotelDataPlugin.KEY ? false
        : target == key ? key != IHotelDataPlugin.KEY
        : throw new ArgumentException(
            $"A style of {key} is changed for this hotel or in {key}'s own files, not in {target}.",
            nameof(target)
        );

    private static string Into(string key, string? target) =>
        string.IsNullOrEmpty(target) || target == IHotelDataPlugin.KEY
            ? string.Empty
            : $" into {target}";

    /// <summary>The theme's style default written into its <c>theme.json</c>, and the theme loaded again.</summary>
    private async Task<PluginStyle> SetDefaultAsync(
        string key,
        string name,
        string? value,
        CancellationToken ct
    )
    {
        var style = await Styles
            .SetDefaultAsync(key, Hotel.EditableFolderOf(key), name, value, ct)
            .ConfigureAwait(false);

        await Hotel.ReloadAsync(key, ct).ConfigureAwait(false);

        return style;
    }

    /// <summary>A theme staff work on is loaded again after its files changed; the hotel's own values need none.</summary>
    private Task ReloadThemeAsync(string key, CancellationToken ct) =>
        key == IHotelDataPlugin.KEY ? Task.CompletedTask : Hotel.ReloadAsync(key, ct);

    /// <summary>Lets the request body be an image's size; null, or the refusal of a body that says it is bigger.</summary>
    private static IResult? ImageLimit(HttpContext http)
    {
        if (http.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
            limit.MaxRequestBodySize = IMAGE_MAX_BYTES + 64 * 1024;

        return http.Request.ContentLength > IMAGE_MAX_BYTES + 64 * 1024
            ? AdminResults.Error(StatusCodes.Status413PayloadTooLarge, "An image is at most 2 MB.")
            : null;
    }

    private async Task<IResult> PatchesAsync(string key, HttpContext http, CancellationToken ct)
    {
        if (hotelService is null)
            return AdminResults.Error(StatusCodes.Status404NotFound, "The server runs no plugins.");

        try
        {
            return Results.Ok(
                new HotelPatchesResponse(
                    key,
                    await Hotel.PatchesAsync(key, ct).ConfigureAwait(false),
                    await HoldsAsync(http, PermissionNodes.Admin.PLUGINS_MANAGE, ct)
                        .ConfigureAwait(false)
                )
            );
        }
        catch (KeyNotFoundException ex)
        {
            return AdminResults.Error(StatusCodes.Status404NotFound, ex.Message);
        }
        catch (ArgumentException ex)
        {
            return AdminResults.Error(StatusCodes.Status422UnprocessableEntity, ex.Message);
        }
    }

    private Task<IResult> SavePatchAsync(
        string key,
        HotelPatch patch,
        HttpContext http,
        CancellationToken ct
    ) =>
        ManageAsync(
            http,
            $"patch {key} {patch.Template} {patch.Name}{(patch.Disabled ? " (disabled)" : "")}",
            ct,
            async _ => Results.Ok(await Hotel.SavePatchAsync(key, patch, ct).ConfigureAwait(false))
        );

    private Task<IResult> RemovePatchAsync(
        string key,
        string id,
        HttpContext http,
        CancellationToken ct
    ) =>
        ManageAsync(
            http,
            $"patch {key} {id} removed",
            ct,
            async _ =>
                await Hotel.RemovePatchAsync(key, id, ct).ConfigureAwait(false)
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
    bool Changed,
    string? Origin
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
            style.Changed,
            style.Property.From
        );
}

/// <summary>A theme's style properties; <c>canManage</c>: may change them.</summary>
internal sealed record PluginStylesResponse(
    string Key,
    string? Extends,
    ImmutableArray<PluginStyleView> Styles,
    bool CanManage
);

/// <summary>
/// A plugin that is a theme: the theme it extends (null: none), its style properties, those the
/// hotel changed and those it declares itself (the rest are its parent's); <c>error</c> why its
/// <c>theme.json</c> can't be read.
/// </summary>
internal sealed record PluginThemeView(
    string Key,
    string? Name,
    string State,
    string? Extends,
    int Styles,
    int Changed,
    int Own,
    string? Error
);

/// <summary>The themes, under the base theme <c>base</c>; <c>canManage</c>: may change their styles.</summary>
internal sealed record PluginThemesResponse(
    string Base,
    ImmutableArray<PluginThemeView> Themes,
    bool CanManage
);

/// <summary>A plugin's <c>wwwroot/template-patches.json</c>, as its clients are given it.</summary>
internal sealed record PluginTemplatePatchesResponse(
    string Key,
    ImmutableArray<PluginTemplatePatch> Patches
);

/// <summary>The images in a plugin's <c>wwwroot/styles/</c>; <c>canManage</c>: may upload and remove them.</summary>
internal sealed record PluginImagesResponse(
    string Key,
    ImmutableArray<PluginImage> Images,
    bool CanManage
);

/// <summary>The font files in a plugin's <c>wwwroot/styles/</c>; <c>canManage</c>: may upload and remove them.</summary>
internal sealed record PluginFontsResponse(
    string Key,
    ImmutableArray<PluginFont> Fonts,
    bool CanManage
);

/// <summary>A skin set: an image url per <c>asset.overlays</c> key, null to give the library's own back.</summary>
internal sealed record HotelOverlaysInput(Dictionary<string, string?>? Overlays);

/// <summary>The hotel's <c>asset.overlays</c> entries after a skin set.</summary>
internal sealed record HotelOverlaysResponse(ImmutableDictionary<string, string> Overlays);

/// <summary>Texts set in a theme's own files: a text per key, null takes the theme's away.</summary>
internal sealed record ThemeTextsInput(Dictionary<string, string?>? Texts);

/// <summary>The theme's texts after a change.</summary>
internal sealed record ThemeTextsResponse(ImmutableDictionary<string, string> Texts);

/// <summary>The patches staff made in a plugin (the hotel's own, or a theme they work on), and its key.</summary>
internal sealed record HotelPatchesResponse(
    string Key,
    ImmutableArray<HotelPatch> Patches,
    bool CanManage
);
