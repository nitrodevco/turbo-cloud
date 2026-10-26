using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.Admin.Configuration;

namespace Turbo.Admin.Assets;

/// <summary>
/// The two files the client is served beside its page that name its windows (nitro-next
/// <c>tools/skin-map.mjs</c>, <c>docs/template-patches.md</c> "The skin map"): <see cref="SKINS"/>,
/// every skin of the theme with its sprites and the windows drawing it, and <see cref="INDEX"/>,
/// every template window's element tree. Read from the client's page
/// (<see cref="ClientAssets.ClientPage"/>), as the browser resolves the file's name against it,
/// and kept for <see cref="KEEP"/> - the client's build writes them, so they change only with a
/// new client - unless the panel asks for them fresh.
/// </summary>
public sealed class ClientWindows(
    IHttpClientFactory http,
    IOptionsMonitor<AdminConfig> config,
    TimeProvider time,
    ILogger<ClientWindows> logger
)
{
    public const string HTTP_CLIENT = "turbo-admin-client-windows";
    public const string SKINS = "skins.json";
    public const string INDEX = "template-index.json";

    /// <summary>How long a read file is served before it is read again.</summary>
    public static readonly TimeSpan KEEP = TimeSpan.FromMinutes(10);

    private readonly ConcurrentDictionary<string, (DateTimeOffset At, byte[] Json)> _kept = new(
        StringComparer.Ordinal
    );

    /// <summary>
    /// The file (<see cref="SKINS"/> or <see cref="INDEX"/>) as the client is served it, or why it
    /// can't be read: no client page set, or the client answering none (a build that could not
    /// reach the template bundles leaves them out).
    /// </summary>
    public async Task<ClientWindowsFile> GetAsync(string file, bool fresh, CancellationToken ct)
    {
        if (ClientAssets.ClientPage(config.CurrentValue) is not { } page)
            return new(
                "",
                null,
                "The panel doesn't know where the client is: set Turbo:Admin:ClientPreviewUrl or ClientLoginUrl under Settings."
            );

        var url = new Uri(page, file).AbsoluteUri;
        var now = time.GetUtcNow();

        if (!fresh && _kept.TryGetValue(url, out var kept) && now - kept.At < KEEP)
            return new(url, kept.Json, null);

        try
        {
            using var response = await http.CreateClient(HTTP_CLIENT)
                .GetAsync(url, ct)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                return new(
                    url,
                    null,
                    $"The client answered {(int)response.StatusCode} for {url}: its build writes {file} only when it reached the template bundles."
                );

            var json = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);

            _kept[url] = (now, json);

            return new(url, json, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            if (ct.IsCancellationRequested)
                throw;

            logger.LogWarning(ex, "The client's {File} could not be read from {Url}", file, url);

            return new(url, null, $"{url} could not be read: {ex.Message}");
        }
    }
}

/// <summary>A file of <see cref="ClientWindows"/>: where it was read, its JSON, or why there is none.</summary>
public sealed record ClientWindowsFile(string Url, byte[]? Json, string? Problem);
