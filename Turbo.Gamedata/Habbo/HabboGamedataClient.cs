using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.Gamedata.Configuration;

namespace Turbo.Gamedata.Habbo;

/// <summary>
/// Habbo's own gamedata, from <c>www.habbo.&lt;domain&gt;/gamedata/&lt;file&gt;/0</c> (or where
/// <see cref="GamedataConfig.HabboGamedataUrl"/> says), which redirects to the file's current
/// hash. What is transient (a dropped connection, a timeout, a 5xx, a 429) is asked again with a
/// growing delay; Habbo's DDoS filter answers a request it refuses with a page of HTML, so an
/// answer that is not the file is a failure, not data.
/// </summary>
internal sealed class HabboGamedataClient(
    IHttpClientFactory httpClients,
    ILogger<HabboGamedataClient> logger,
    IOptions<GamedataConfig>? config = null
)
{
    public const string HTTP_CLIENT = "habbo-gamedata";

    // Habbo's filter refuses a browser's agent from a server, and a request that names none.
    public const string USER_AGENT = "Turbo";

    private const string DEFAULT_URL = "https://www.habbo.{domain}/gamedata/{file}/0";

    private const int MAX_ATTEMPTS = 4;
    private static readonly TimeSpan RETRY_DELAY = TimeSpan.FromMilliseconds(750);

    /// <summary>The <c>key=value</c> lines of its external variables; a key given twice keeps the last.</summary>
    public async Task<IReadOnlyDictionary<string, string>> GetExternalVariablesAsync(
        string domain,
        CancellationToken ct
    ) => ParseVariables(await GetExternalVariablesFileAsync(domain, ct).ConfigureAwait(false));

    /// <summary>
    /// Its external variables, as served: <c>key=value</c> lines. Throws
    /// <see cref="HttpRequestException"/> when there are none, as when the filter refused the
    /// request.
    /// </summary>
    public async Task<byte[]> GetExternalVariablesFileAsync(string domain, CancellationToken ct)
    {
        var bytes = await GetAsync(domain, "external_variables", ct).ConfigureAwait(false);

        if (ParseVariables(bytes).Count == 0)
            throw new HttpRequestException(
                $"habbo.{domain}'s external variables came back empty or not as variables."
            );

        return bytes;
    }

    /// <summary>
    /// Each file's hash as Habbo's <c>/gamedata/hashes</c> lists it (<c>{ "hashes": [ { name, url,
    /// hash } ] }</c>), by its name there: <c>furnidata</c>, <c>productdata</c>,
    /// <c>external_texts</c>, <c>external_variables</c>, <c>figurepartlist</c>. Its address is the
    /// gamedata's with <c>hashes</c> for the file (and no <c>/0</c>, which it does not answer).
    /// </summary>
    public async Task<IReadOnlyDictionary<string, string>> GetHashesAsync(
        string domain,
        CancellationToken ct
    )
    {
        var url = (config?.Value.HabboGamedataUrl ?? DEFAULT_URL)
            .Replace("{domain}", domain, StringComparison.Ordinal)
            .Replace("{file}/0", "hashes", StringComparison.Ordinal)
            .Replace("{file}", "hashes", StringComparison.Ordinal);
        var hashes = new Dictionary<string, string>(StringComparer.Ordinal);

        try
        {
            using var document = JsonDocument.Parse(
                await GetUrlAsync(url, ct).ConfigureAwait(false)
            );

            if (
                document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("hashes", out var list)
                || list.ValueKind != JsonValueKind.Array
            )
                throw new HttpRequestException($"{url} lists no hashes.");

            foreach (var entry in list.EnumerateArray())
                if (
                    entry.ValueKind == JsonValueKind.Object
                    && entry.TryGetProperty("name", out var name)
                    && entry.TryGetProperty("hash", out var hash)
                    && name.ValueKind == JsonValueKind.String
                    && hash.ValueKind == JsonValueKind.String
                )
                    hashes[name.GetString()!] = hash.GetString()!;
        }
        catch (JsonException ex)
        {
            throw new HttpRequestException(
                $"{url} is not JSON; Habbo's filter may have refused the request.",
                ex
            );
        }

        return hashes;
    }

    /// <summary>The <c>key=value</c> lines of an external variables file; a key given twice keeps the last.</summary>
    public static Dictionary<string, string> ParseVariables(byte[] file)
    {
        var text = Encoding.UTF8.GetString(file);
        var variables = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var line in text.Split('\n'))
        {
            var separator = line.IndexOf('=');

            if (separator <= 0 || line.StartsWith('#'))
                continue;

            variables[line[..separator].Trim()] = line[(separator + 1)..].Trim();
        }

        return variables;
    }

    /// <summary>Its furniture data, as served.</summary>
    public async Task<byte[]> GetFurnitureDataAsync(string domain, CancellationToken ct)
    {
        var bytes = await GetAsync(domain, "furnidata_json", ct).ConfigureAwait(false);

        try
        {
            using var document = JsonDocument.Parse(bytes);

            if (
                document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("roomitemtypes", out _)
            )
                throw new HttpRequestException(
                    $"habbo.{domain}'s furniture data has no roomitemtypes."
                );
        }
        catch (JsonException ex)
        {
            throw new HttpRequestException(
                $"habbo.{domain}'s furniture data is not JSON; its filter may have refused the request.",
                ex
            );
        }

        return bytes;
    }

    /// <summary>Its product data, as served: JSON with a <c>productdata</c> list.</summary>
    public async Task<byte[]> GetProductDataAsync(string domain, CancellationToken ct)
    {
        var bytes = await GetAsync(domain, "productdata_json", ct).ConfigureAwait(false);

        try
        {
            using var document = JsonDocument.Parse(bytes);

            if (!document.RootElement.TryGetProperty("productdata", out _))
                throw new HttpRequestException(
                    $"habbo.{domain}'s product data has no productdata."
                );
        }
        catch (JsonException ex)
        {
            throw new HttpRequestException(
                $"habbo.{domain}'s product data is not JSON; its filter may have refused the request.",
                ex
            );
        }

        return bytes;
    }

    /// <summary>Its figure data, as served: XML, a <c>&lt;figuredata&gt;</c>.</summary>
    public async Task<byte[]> GetFigureDataAsync(string domain, CancellationToken ct)
    {
        var bytes = await GetAsync(domain, "figuredata", ct).ConfigureAwait(false);
        var head = Encoding.UTF8.GetString(bytes, 0, Math.Min(bytes.Length, 512));

        // The filter's refusal is a page of HTML.
        if (!head.Contains("<figuredata", StringComparison.Ordinal))
            throw new HttpRequestException(
                $"habbo.{domain}'s figure data is not figure data; its filter may have refused the request."
            );

        return bytes;
    }

    /// <summary>Its external texts, as served: <c>key=value</c> lines.</summary>
    public async Task<byte[]> GetExternalTextsAsync(string domain, CancellationToken ct)
    {
        var bytes = await GetAsync(domain, "external_flash_texts", ct).ConfigureAwait(false);
        var head = Encoding.UTF8.GetString(bytes, 0, Math.Min(bytes.Length, 512)).TrimStart();

        // The filter's refusal is a page of HTML; texts are lines with an = in them.
        if (head.StartsWith('<') || !head.Contains('=', StringComparison.Ordinal))
            throw new HttpRequestException(
                $"habbo.{domain}'s external texts are not texts; its filter may have refused the request."
            );

        return bytes;
    }

    /// <summary>
    /// A furniture's asset file; null when Habbo has none at that address (a 404), which happens
    /// for furniture it lists but no longer serves.
    /// </summary>
    public Task<byte[]?> GetFurnitureFileAsync(string url, CancellationToken ct) =>
        GetFileAsync(url, ct);

    /// <summary>
    /// A file on Habbo's hosts (a client library, the effect map); null when Habbo has none at that
    /// address (a 404 or 403). Anything else that fails throws once the retries are spent.
    /// </summary>
    public async Task<byte[]?> GetFileAsync(string url, CancellationToken ct)
    {
        try
        {
            return await GetUrlAsync(url, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
            when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden)
        {
            return null;
        }
    }

    /// <summary>
    /// Whether Habbo has a file at the address, asked without downloading it: a HEAD, or - where
    /// HEAD is refused - the first byte. Null when no answer came, after the retries.
    /// </summary>
    public async Task<bool?> ExistsAsync(string url, CancellationToken ct)
    {
        var http = httpClients.CreateClient(HTTP_CLIENT);
        var head = true;

        for (var attempt = 1; attempt <= MAX_ATTEMPTS; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(
                    head ? HttpMethod.Head : HttpMethod.Get,
                    url
                );

                if (!head)
                    request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(0, 0);

                using var response = await http.SendAsync(
                        request,
                        HttpCompletionOption.ResponseHeadersRead,
                        ct
                    )
                    .ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                    return true;

                if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
                    return false;

                if (
                    head
                    && response.StatusCode
                        is HttpStatusCode.Forbidden
                            or HttpStatusCode.MethodNotAllowed
                )
                {
                    head = false;
                    attempt--;

                    continue;
                }

                if (!IsTransient(response.StatusCode))
                    return response.StatusCode == HttpStatusCode.Forbidden ? false : null;
            }
            catch (HttpRequestException) { }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested) { }

            if (attempt < MAX_ATTEMPTS)
                await Task.Delay(RETRY_DELAY * attempt, ct).ConfigureAwait(false);
        }

        return null;
    }

    private Task<byte[]> GetAsync(string domain, string file, CancellationToken ct) =>
        GetUrlAsync(
            (config?.Value.HabboGamedataUrl ?? DEFAULT_URL)
                .Replace("{domain}", domain, StringComparison.Ordinal)
                .Replace("{file}", file, StringComparison.Ordinal),
            ct
        );

    private async Task<byte[]> GetUrlAsync(string url, CancellationToken ct)
    {
        var http = httpClients.CreateClient(HTTP_CLIENT);
        Exception? last = null;

        for (var attempt = 1; attempt <= MAX_ATTEMPTS; attempt++)
        {
            try
            {
                using var response = await http.GetAsync(url, ct).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                    return await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);

                last = new HttpRequestException(
                    $"{url} answered {(int)response.StatusCode}.",
                    null,
                    response.StatusCode
                );

                if (!IsTransient(response.StatusCode))
                    break;
            }
            catch (HttpRequestException ex)
            {
                last = ex;
            }
            catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
            {
                // HttpClient's own timeout, not the caller giving up.
                last = ex;
            }

            if (attempt < MAX_ATTEMPTS)
            {
                logger.LogWarning(
                    last,
                    "Habbo gamedata {Url} failed on attempt {Attempt}; trying again",
                    url,
                    attempt
                );

                await Task.Delay(RETRY_DELAY * attempt, ct).ConfigureAwait(false);
            }
        }

        throw last as HttpRequestException ?? new HttpRequestException($"{url} failed.", last);
    }

    private static bool IsTransient(HttpStatusCode status) =>
        status is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests
        || (int)status >= 500;
}
