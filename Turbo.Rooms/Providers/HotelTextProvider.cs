using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.Primitives.Texts;
using Turbo.Rooms.Configuration;

namespace Turbo.Rooms.Providers;

/// <summary>
/// The hotel's texts, read from the client's <c>ExternalTexts.json</c> (a flat map of key to
/// text). The hotel names either a file or an address, because the same file is usually served
/// beside the rest of the client's assets. Its only caller today is the wired variables, which
/// name hand items, effects, dances and signs for the editor; move it out of the room module
/// when a second module wants texts, because they belong to the hotel and not to a room.
///
/// A text may point at another (<c>wiredfurni.params.action.dance.1</c> is
/// <c>${widget.memenu.dance1}</c>), so a key is followed to the end before it is handed out.
/// </summary>
public sealed class HotelTextProvider(
    IOptions<HotelTextConfig> config,
    ILogger<IHotelTextProvider> logger
) : IHotelTextProvider, IDisposable
{
    private const string USER_AGENT = "Turbo";

    private const string KEY_OPEN = "${";
    private const char KEY_CLOSE = '}';

    private readonly HotelTextConfig _config = config.Value;
    private readonly ILogger<IHotelTextProvider> _logger = logger;

    // One client for the life of the provider, which is the life of the emulator. Asset hosts
    // commonly refuse a request that names no agent, and HttpClient sends none of its own.
    private readonly HttpClient _http = new()
    {
        DefaultRequestHeaders = { { "User-Agent", USER_AGENT } },
        Timeout = TimeSpan.FromSeconds(config.Value.FetchTimeoutSeconds),
    };

    private ImmutableDictionary<string, string> _texts = ImmutableDictionary<string, string>.Empty;

    public bool TryGetText(string key, out string text)
    {
        text = string.Empty;

        if (!_texts.TryGetValue(key, out var found))
            return false;

        text = found;

        return text.Length > 0;
    }

    public async Task ReloadAsync(CancellationToken ct = default)
    {
        var source = _config.ExternalTextsPath;

        if (string.IsNullOrWhiteSpace(source))
            return;

        Dictionary<string, string>? raw;

        try
        {
            await using var stream = await OpenAsync(source, ct).ConfigureAwait(false);

            raw = await JsonSerializer
                .DeserializeAsync<Dictionary<string, string>>(stream, cancellationToken: ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // The hotel named something the server cannot read: say so and run without texts
            // rather than refusing to start over a cosmetic feature.
            _logger.LogError(ex, "Failed to read the hotel texts from {Source}", source);

            return;
        }

        if (raw is null || raw.Count == 0)
        {
            _logger.LogWarning("The hotel texts at {Source} are empty", source);

            return;
        }

        var resolved = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);

        foreach (var (key, value) in raw)
            resolved[key] = Resolve(raw, value, _config.MaxKeyDepth);

        _texts = resolved.ToImmutable();

        _logger.LogInformation("Read {Count} hotel texts from {Source}", _texts.Count, source);
    }

    public void Dispose() => _http.Dispose();

    /// <summary>An address is fetched, anything else is a file beside the server.</summary>
    private async Task<Stream> OpenAsync(string source, CancellationToken ct)
    {
        if (
            !Uri.TryCreate(source, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        )
            return File.OpenRead(source);

        var response = await _http.GetAsync(uri, ct).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// A text that is nothing but a reference to another one is replaced by what it points at.
    /// A chain that runs too deep, or points nowhere, is left as it was written.
    /// </summary>
    private static string Resolve(Dictionary<string, string> raw, string value, int maxDepth)
    {
        for (var depth = 0; depth < maxDepth; depth++)
        {
            if (!value.StartsWith(KEY_OPEN, StringComparison.Ordinal))
                return value;

            var end = value.IndexOf(KEY_CLOSE);

            // Only a text that is exactly one reference is followed; one that merely contains
            // a reference is the client's to expand.
            if (end != value.Length - 1 || !raw.TryGetValue(value[2..end], out var next))
                return value;

            value = next;
        }

        return value;
    }
}
