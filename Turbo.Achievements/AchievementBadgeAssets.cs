using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.Achievements.Configuration;

namespace Turbo.Achievements;

/// <summary>
/// Whether a badge image exists where the hotel serves it: at
/// <see cref="AchievementConfig.BadgeAssetUrl"/> when one is set, otherwise as a PNG in
/// <see cref="AchievementConfig.BadgeAssetDirectory"/>. Validation cannot wait on the network,
/// so <see cref="CheckAsync"/> looks the badges up first and <see cref="Exists"/> answers from
/// what it found. A badge found once is not asked for again; a missing one is asked for again
/// after <see cref="MISSING_RETRY"/>, so a badge uploaded later is picked up.
/// </summary>
public sealed class AchievementBadgeAssets
{
    public const string BADGE_NAME_TOKEN = "%badgename%";

    private const string USER_AGENT = "Turbo";
    private const int MAX_CONCURRENT_REQUESTS = 8;
    private static readonly TimeSpan REQUEST_TIMEOUT = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan MISSING_RETRY = TimeSpan.FromMinutes(1);

    private readonly AchievementConfig _config;
    private readonly TimeProvider _time;
    private readonly ILogger<AchievementBadgeAssets>? _logger;
    private readonly HttpClient _http;
    private readonly ConcurrentDictionary<string, byte> _found = new(
        StringComparer.OrdinalIgnoreCase
    );
    private readonly ConcurrentDictionary<string, DateTimeOffset> _missingUntil = new(
        StringComparer.OrdinalIgnoreCase
    );

    /// <param name="handler">Sends the requests; tests replace it, the server leaves it null.</param>
    public AchievementBadgeAssets(
        IOptions<AchievementConfig> config,
        TimeProvider? time = null,
        ILogger<AchievementBadgeAssets>? logger = null,
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

    private bool UsesUrl => !string.IsNullOrWhiteSpace(_config.BadgeAssetUrl);

    /// <summary>An absolute http(s) URL with the badge name token in it.</summary>
    public static bool IsValidUrlTemplate(string template) =>
        template.Contains(BADGE_NAME_TOKEN, StringComparison.Ordinal)
        && Uri.TryCreate(
            template.Replace(BADGE_NAME_TOKEN, "badge", StringComparison.Ordinal),
            UriKind.Absolute,
            out var uri
        )
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    /// <summary>Where the badge's image is expected: its URL, or its file name in the directory.</summary>
    public string Describe(string badgeCode) =>
        UsesUrl
            ? _config.BadgeAssetUrl.Replace(
                BADGE_NAME_TOKEN,
                Uri.EscapeDataString(badgeCode),
                StringComparison.Ordinal
            )
            : badgeCode + ".png";

    /// <summary>Looks up every badge not already known, so <see cref="Exists"/> can answer for them.</summary>
    public async Task CheckAsync(IEnumerable<string> badgeCodes, CancellationToken ct)
    {
        if (!UsesUrl)
            return;
        var now = _time.GetUtcNow();
        var unknown = badgeCodes
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(code =>
                !_found.ContainsKey(code)
                && !(_missingUntil.TryGetValue(code, out var until) && until > now)
            )
            .ToArray();
        if (unknown.Length == 0)
            return;
        var failures = 0;
        await Parallel
            .ForEachAsync(
                unknown,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = MAX_CONCURRENT_REQUESTS,
                    CancellationToken = ct,
                },
                async (code, token) =>
                {
                    if (await FetchAsync(code, token).ConfigureAwait(false))
                    {
                        _found.TryAdd(code, 0);
                        _missingUntil.TryRemove(code, out _);
                    }
                    else
                    {
                        _missingUntil[code] = _time.GetUtcNow() + MISSING_RETRY;
                    }
                }
            )
            .ConfigureAwait(false);
        var missing = unknown.Count(code => !_found.ContainsKey(code));
        _logger?.LogInformation(
            "Checked {Count} badge images at {Url}: {Missing} missing",
            unknown.Length,
            _config.BadgeAssetUrl,
            missing
        );

        async Task<bool> FetchAsync(string code, CancellationToken token)
        {
            try
            {
                // Headers only: the status is the answer, and the image itself is not needed.
                using var response = await _http
                    .GetAsync(Describe(code), HttpCompletionOption.ResponseHeadersRead, token)
                    .ConfigureAwait(false);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
                when (ex is HttpRequestException
                    || (ex is TaskCanceledException && !token.IsCancellationRequested)
                )
            {
                if (Interlocked.Increment(ref failures) == 1)
                    _logger?.LogWarning(
                        ex,
                        "Could not reach the badge image {Url}; it counts as missing",
                        Describe(code)
                    );
                return false;
            }
        }
    }

    /// <summary>
    /// Whether the badge's image exists. With a URL this is what <see cref="CheckAsync"/> found, so
    /// a badge it was never asked about counts as missing.
    /// </summary>
    public bool Exists(string badgeCode) =>
        UsesUrl
            ? _found.ContainsKey(badgeCode)
            : !string.IsNullOrWhiteSpace(_config.BadgeAssetDirectory)
                && File.Exists(Path.Combine(_config.BadgeAssetDirectory, badgeCode + ".png"));
}
