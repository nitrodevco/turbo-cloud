using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.Admin.Api.Contracts;
using Turbo.Admin.Configuration;
using Turbo.Primitives.Gamedata;

namespace Turbo.Admin.Assets;

/// <summary>
/// The client's image addresses, read from the external variables the hotel serves it
/// (<see cref="GamedataFiles.EXTERNAL_VARIABLES"/>) so the panel draws the same pictures players
/// see. They are written as the client resolves them: <c>${key}</c> is another key's value, and
/// an address that names no host is under the client's page (<see cref="AdminConfig.ClientLoginUrl"/>);
/// without one, such an address is left out. Read again whenever the variables are built anew.
/// </summary>
public sealed class ClientAssets(
    IGamedataFileService files,
    IOptions<AdminConfig> config,
    ILogger<ClientAssets> logger
)
{
    public const string CATALOG_ICON = "catalog.icons.url";
    public const string CATALOG_IMAGE = "asset.urls.catalog";
    public const string FURNI_ICON = "asset.urls.icons.furni";
    public const string BADGE = "badge.asset.url";
    public const string IMAGE_LIBRARY = "image.library.url";

    private const string KEY_OPEN = "${";
    private const char KEY_CLOSE = '}';

    // As many rounds as the client's own `interpolate` makes.
    private const int MAX_KEY_DEPTH = 3;

    private readonly AdminConfig _config = config.Value;

    private (string Hash, ClientAssetsResponse Assets) _read = ("", ClientAssetsResponse.NONE);

    public async Task<ClientAssetsResponse> GetAsync(CancellationToken ct)
    {
        try
        {
            var current = await files
                .GetCurrentAsync(GamedataFiles.EXTERNAL_VARIABLES, ct)
                .ConfigureAwait(false);
            var read = _read;

            if (current.File.Hash == read.Hash)
                return read.Assets;

            using var content = new GZipStream(
                new MemoryStream(current.Gzipped),
                CompressionMode.Decompress
            );
            var assets = Read(content);

            _read = (current.File.Hash, assets);

            return assets;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // Pictures are a convenience: the panel works without them, so keep what was read last.
            logger.LogWarning(ex, "Could not read the client's external variables");

            return _read.Assets;
        }
    }

    private ClientAssetsResponse Read(Stream content)
    {
        using var document = JsonDocument.Parse(content);
        var values = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var property in document.RootElement.EnumerateObject())
            if (property.Value.ValueKind == JsonValueKind.String)
                values[property.Name] = property.Value.GetString() ?? "";

        var page =
            Uri.TryCreate(_config.ClientLoginUrl, UriKind.Absolute, out var url)
            && (url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps)
                ? url
                : null;

        string Value(string key) =>
            values.TryGetValue(key, out var value)
                ? Absolute(page, Interpolate(values, value))
                : "";

        return new ClientAssetsResponse(
            Value(CATALOG_ICON),
            Value(CATALOG_IMAGE),
            Value(FURNI_ICON),
            Value(BADGE),
            Value(IMAGE_LIBRARY)
        );
    }

    /// <summary>Every <c>${key}</c> the config has a value for, replaced, as the client does.</summary>
    private static string Interpolate(Dictionary<string, string> values, string text)
    {
        for (var round = 0; round < MAX_KEY_DEPTH; round++)
        {
            var replaced = false;
            var start = text.IndexOf(KEY_OPEN, StringComparison.Ordinal);

            while (start >= 0)
            {
                var end = text.IndexOf(KEY_CLOSE, start + KEY_OPEN.Length);

                if (end < 0)
                    break;

                var key = text[(start + KEY_OPEN.Length)..end];

                if (values.TryGetValue(key, out var value))
                {
                    text = string.Concat(text.AsSpan(0, start), value, text.AsSpan(end + 1));
                    replaced = true;
                    start = text.IndexOf(KEY_OPEN, start + value.Length, StringComparison.Ordinal);
                }
                else
                {
                    start = text.IndexOf(KEY_OPEN, end + 1, StringComparison.Ordinal);
                }
            }

            if (!replaced)
                break;
        }

        return text;
    }

    /// <summary>
    /// An address the browser would read beside the client, made one the panel can read: under
    /// the page's scheme when it starts <c>//</c>, its host when it starts <c>/</c>, and its
    /// folder when it names neither. Built by hand, because <see cref="Uri"/> would escape the
    /// <c>%name%</c> placeholders. Without the page, only an address with its own host is kept.
    /// </summary>
    private static string Absolute(Uri? source, string address)
    {
        if (address.Length == 0 || address.Contains("://", StringComparison.Ordinal))
            return address;

        if (source is null)
            return "";

        if (address.StartsWith("//", StringComparison.Ordinal))
            return $"{source.Scheme}:{address}";

        var origin = source.GetLeftPart(UriPartial.Authority);

        if (address.StartsWith('/'))
            return origin + address;

        var path = source.AbsolutePath;

        return origin + path[..(path.LastIndexOf('/') + 1)] + address;
    }
}
