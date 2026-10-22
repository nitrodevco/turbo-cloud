using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.Gamedata.Configuration;

namespace Turbo.Gamedata.Assets.Publishing;

/// <summary>
/// Asks Cloudflare to drop its cached copies of what a publish sent (<c>POST
/// zones/{zone}/purge_cache</c> with <c>files</c>), <see cref="AssetBundleConfig.CloudflarePurgeBatch"/>
/// addresses a request, so a file that was missing is not served from the cache as missing for
/// hours after it is published. The token is sent only to <see cref="AssetBundleConfig.CloudflareApiUrl"/>
/// and is never logged.
/// </summary>
internal sealed class CloudflarePurge(
    IHttpClientFactory httpClients,
    IOptions<AssetBundleConfig> config,
    ILogger<CloudflarePurge> logger
)
{
    public const string HTTP_CLIENT = "cloudflare-purge";

    private const int MAX_BATCH = 30;

    /// <summary>
    /// The addresses <paramref name="paths"/> are reached at under <paramref name="publicUrl"/>,
    /// each part of a path escaped as a browser asks for it.
    /// </summary>
    public static IReadOnlyList<string> UrlsOf(string publicUrl, IEnumerable<string> paths)
    {
        var root = publicUrl.Trim().TrimEnd('/');

        return
        [
            .. paths
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .Select(path =>
                    $"{root}/{string.Join('/', path.Split('/').Select(Uri.EscapeDataString))}"
                ),
        ];
    }

    /// <summary>
    /// Purges <paramref name="urls"/> from <paramref name="zoneId"/>'s cache: how many were
    /// purged, and why the first batch that failed did (the rest are still asked).
    /// </summary>
    public async Task<CloudflarePurgeResult> PurgeAsync(
        string zoneId,
        string token,
        IReadOnlyList<string> urls,
        Action<string> log,
        CancellationToken ct
    )
    {
        var purged = 0;
        string? error = null;
        var batch = Math.Clamp(config.Value.CloudflarePurgeBatch, 1, MAX_BATCH);
        var endpoint =
            $"{config.Value.CloudflareApiUrl.TrimEnd('/')}/zones/{Uri.EscapeDataString(zoneId)}/purge_cache";
        var http = httpClients.CreateClient(HTTP_CLIENT);

        foreach (var files in urls.Chunk(batch))
        {
            ct.ThrowIfCancellationRequested();

            var failure = await PurgeBatchAsync(http, endpoint, token, files, ct)
                .ConfigureAwait(false);

            if (failure is null)
            {
                purged += files.Length;

                continue;
            }

            error ??= failure;
            log($"Cloudflare did not purge {files.Length} addresses: {failure}");
        }

        return new CloudflarePurgeResult(purged, error);
    }

    /// <summary>Null when Cloudflare purged them, else why it did not.</summary>
    private async Task<string?> PurgeBatchAsync(
        HttpClient http,
        string endpoint,
        string token,
        string[] files,
        CancellationToken ct
    )
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = JsonContent.Create(new PurgeRequest(files)),
            };

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await http.SendAsync(request, ct).ConfigureAwait(false);
            PurgeResponse? body = null;

            try
            {
                body = await response
                    .Content.ReadFromJsonAsync<PurgeResponse>(ct)
                    .ConfigureAwait(false);
            }
            catch (JsonException) { }

            if (response.IsSuccessStatusCode && body?.Success == true)
                return null;

            var said = body?.Errors is { Length: > 0 } errors
                ? string.Join("; ", errors.Select(x => $"{x.Message} ({x.Code})"))
                : null;

            return said is null
                ? $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}"
                : $"HTTP {(int)response.StatusCode}: {said}";
        }
        catch (Exception ex)
            when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Purging {Count} addresses from Cloudflare failed", files.Length);

            return ex.Message;
        }
    }

    private sealed record PurgeRequest([property: JsonPropertyName("files")] string[] Files);

    private sealed record PurgeResponse(
        [property: JsonPropertyName("success")] bool Success,
        [property: JsonPropertyName("errors")] PurgeError[]? Errors
    );

    private sealed record PurgeError(
        [property: JsonPropertyName("code")] int Code,
        [property: JsonPropertyName("message")] string? Message
    );
}

/// <summary>A target's purge after each publish: the zone, and its token unsealed.</summary>
internal sealed record AssetPublishPurge(CloudflarePurge Purger, string ZoneId, string Token)
{
    /// <summary>Never the token.</summary>
    public override string ToString() => $"Cloudflare zone {ZoneId}";
}

/// <summary>How many addresses a purge cleared, and why one failed when one did.</summary>
internal sealed record CloudflarePurgeResult(int Purged, string? Error);
