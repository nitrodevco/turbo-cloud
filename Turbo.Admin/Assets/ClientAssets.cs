using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.Admin.Api.Contracts;
using Turbo.Admin.Configuration;

namespace Turbo.Admin.Assets;

/// <summary>
/// The client's image addresses, read from its <c>nitro-config.json</c>
/// (<see cref="AdminConfig.ClientConfigUrl"/>) so the panel draws the same pictures players see.
/// They are written as the client resolves them: <c>${key}</c> is another key's value, an address
/// starting <c>//</c> or <c>/</c> is under the config's own host. Read again after
/// <see cref="AdminConfig.ClientConfigCacheMinutes"/>; when the config can't be read, the last
/// addresses read are kept, and it is tried again after <see cref="RETRY"/>.
/// </summary>
public sealed class ClientAssets
{
    public const string CATALOG_ICON = "catalog.icons.url";
    public const string CATALOG_IMAGE = "asset.urls.catalog";
    public const string FURNI_ICON = "asset.urls.icons.furni";
    public const string BADGE = "badge.asset.url";

    private const string USER_AGENT = "Turbo";
    private const string KEY_OPEN = "${";
    private const char KEY_CLOSE = '}';

    // As many rounds as the client's own `interpolate` makes.
    private const int MAX_KEY_DEPTH = 3;

    private static readonly TimeSpan REQUEST_TIMEOUT = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan RETRY = TimeSpan.FromMinutes(1);

    private readonly AdminConfig _config;
    private readonly TimeProvider _time;
    private readonly ILogger<ClientAssets>? _logger;
    private readonly HttpClient _http;

    private ClientAssetsResponse _assets = ClientAssetsResponse.NONE;
    private DateTimeOffset _readAgainAt = DateTimeOffset.MinValue;

    /// <param name="handler">Sends the requests; tests replace it, the server leaves it null.</param>
    public ClientAssets(
        IOptions<AdminConfig> config,
        TimeProvider? time = null,
        ILogger<ClientAssets>? logger = null,
        HttpMessageHandler? handler = null
    )
    {
        _config = config.Value;
        _time = time ?? TimeProvider.System;
        _logger = logger;
        // Asset hosts commonly refuse a request that names no agent, and HttpClient sends none.
        _http = handler is null ? new HttpClient() : new HttpClient(handler);
        _http.DefaultRequestHeaders.Add("User-Agent", USER_AGENT);
        _http.Timeout = REQUEST_TIMEOUT;
    }

    public async Task<ClientAssetsResponse> GetAsync(CancellationToken ct)
    {
        if (
            !Uri.TryCreate(_config.ClientConfigUrl, UriKind.Absolute, out var source)
            || (source.Scheme != Uri.UriSchemeHttp && source.Scheme != Uri.UriSchemeHttps)
        )
            return ClientAssetsResponse.NONE;

        var now = _time.GetUtcNow();

        if (now < _readAgainAt)
            return _assets;

        try
        {
            using var response = await _http.GetAsync(source, ct).ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            using var stream = await response
                .Content.ReadAsStreamAsync(ct)
                .ConfigureAwait(false);
            using var document = await JsonDocument
                .ParseAsync(stream, cancellationToken: ct)
                .ConfigureAwait(false);
            var values = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var property in document.RootElement.EnumerateObject())
                if (property.Value.ValueKind == JsonValueKind.String)
                    values[property.Name] = property.Value.GetString() ?? "";

            string Read(string key) =>
                values.TryGetValue(key, out var value)
                    ? Absolute(source, Interpolate(values, value))
                    : "";

            _assets = new ClientAssetsResponse(
                Read(CATALOG_ICON),
                Read(CATALOG_IMAGE),
                Read(FURNI_ICON),
                Read(BADGE)
            );
            _readAgainAt = now.AddMinutes(_config.ClientConfigCacheMinutes);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // Pictures are a convenience: the panel works without them, so keep what was read
            // last and try again shortly.
            _logger?.LogWarning(ex, "Could not read the client config at {Url}", source);
            _readAgainAt = now.Add(RETRY);
        }

        return _assets;
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
    /// the config's scheme when it starts <c>//</c>, its host when it starts <c>/</c>, and its
    /// folder when it names neither. Built by hand, because <see cref="Uri"/> would escape the
    /// <c>%name%</c> placeholders.
    /// </summary>
    private static string Absolute(Uri source, string address)
    {
        if (address.Length == 0 || address.Contains("://", StringComparison.Ordinal))
            return address;

        if (address.StartsWith("//", StringComparison.Ordinal))
            return $"{source.Scheme}:{address}";

        var origin = source.GetLeftPart(UriPartial.Authority);

        if (address.StartsWith('/'))
            return origin + address;

        var path = source.AbsolutePath;

        return origin + path[..(path.LastIndexOf('/') + 1)] + address;
    }
}
