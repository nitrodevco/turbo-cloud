using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Turbo.Gamedata.Habbo;

/// <summary>
/// Habbo's own gamedata, from <c>www.habbo.&lt;domain&gt;/gamedata/&lt;file&gt;/0</c>, which
/// redirects to the file's current hash. What is transient (a dropped connection, a timeout, a 5xx,
/// a 429) is asked again with a growing delay; Habbo's DDoS filter answers a request it refuses
/// with a page of HTML, so an answer that is not the file is a failure, not data.
/// </summary>
internal sealed class HabboGamedataClient(
    IHttpClientFactory httpClients,
    ILogger<HabboGamedataClient> logger
)
{
    public const string HTTP_CLIENT = "habbo-gamedata";

    // Habbo's filter refuses a browser's agent from a server, and a request that names none.
    public const string USER_AGENT = "Turbo";

    private const int MAX_ATTEMPTS = 4;
    private static readonly TimeSpan RETRY_DELAY = TimeSpan.FromMilliseconds(750);

    /// <summary>The <c>key=value</c> lines of its external variables; a key given twice keeps the last.</summary>
    public async Task<IReadOnlyDictionary<string, string>> GetExternalVariablesAsync(
        string domain,
        CancellationToken ct
    )
    {
        var text = Encoding.UTF8.GetString(
            await GetAsync(domain, "external_variables", ct).ConfigureAwait(false)
        );
        var variables = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var line in text.Split('\n'))
        {
            var separator = line.IndexOf('=');

            if (separator <= 0 || line.StartsWith('#'))
                continue;

            variables[line[..separator].Trim()] = line[(separator + 1)..].Trim();
        }

        if (variables.Count == 0)
            throw new HttpRequestException(
                $"habbo.{domain}'s external variables came back empty or not as variables."
            );

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
    public async Task<byte[]?> GetFurnitureFileAsync(string url, CancellationToken ct)
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

    private Task<byte[]> GetAsync(string domain, string file, CancellationToken ct) =>
        GetUrlAsync($"https://www.habbo.{domain}/gamedata/{file}/0", ct);

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
