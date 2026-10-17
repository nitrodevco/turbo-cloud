using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Turbo.Plugins.Configuration;

namespace Turbo.Plugins.Sources;

/// <summary>A release on GitHub, and the plugin package picked from its assets (null with why not).</summary>
internal sealed record GitHubRelease(
    string Tag,
    string HtmlUrl,
    DateTime? PublishedAtUtc,
    GitHubAsset? Asset,
    string? AssetProblem
);

/// <summary>A release asset: its API url downloads it, for private repositories too.</summary>
internal sealed record GitHubAsset(string Name, string ApiUrl, long Size, string? Sha256);

/// <summary>Why GitHub did not give what was asked, in words for the panel.</summary>
internal sealed class GitHubException(string message) : Exception(message);

/// <summary>
/// GitHub's releases REST API (<see cref="PluginConfig.GitHubApiUrl"/>): a repository's newest
/// release or one by tag, and an asset's bytes. A token, when given, is sent to GitHub only:
/// .NET drops it on the redirect to the asset's storage host.
/// </summary>
internal sealed partial class GitHubReleases(HttpClient http, IOptions<PluginConfig> config)
{
    public const string USER_AGENT = "turbo-cloud-plugins";

    private string ApiUrl => config.Value.GitHubApiUrl.TrimEnd('/');

    /// <summary>
    /// <c>owner/repo</c> from <c>https://github.com/owner/repo(.git)</c>, <c>github.com/owner/repo</c>
    /// or <c>owner/repo</c>; null when it is none of them.
    /// </summary>
    public static string? ParseRepository(string? input)
    {
        var text = RepositoryPrefix().Replace(input?.Trim() ?? "", "");

        if (text.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            text = text[..^4];

        var parts = text.Trim('/').Split('/');

        return
            parts.Length == 2
            && parts.All(p => NamePattern().IsMatch(p) && p is not "." and not "..")
            ? $"{parts[0]}/{parts[1]}"
            : null;
    }

    /// <summary>
    /// The newest release (<paramref name="tag"/> null) or the release tagged <paramref name="tag"/>,
    /// with the package: the asset named <paramref name="assetName"/>, or the release's only zip.
    /// </summary>
    public async Task<GitHubRelease> GetReleaseAsync(
        string repository,
        string? tag,
        string? assetName,
        string? token,
        CancellationToken ct
    )
    {
        var path = tag is null
            ? $"/repos/{repository}/releases/latest"
            : $"/repos/{repository}/releases/tags/{Uri.EscapeDataString(tag)}";

        using var request = Request(ApiUrl + path, token, "application/vnd.github+json");
        using var response = await http.SendAsync(request, ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
            throw new GitHubException(Refusal(response, repository, tag, token is not null));

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false)
        );
        var root = json.RootElement;
        var assets = root.TryGetProperty("assets", out var list)
            ? list.EnumerateArray().Select(ReadAsset).ToList()
            : [];

        GitHubAsset? asset = null;
        string? problem = null;

        if (!string.IsNullOrWhiteSpace(assetName))
        {
            asset = assets.FirstOrDefault(a =>
                string.Equals(a.Name, assetName, StringComparison.OrdinalIgnoreCase)
            );

            if (asset is null)
                problem = $"The release has no asset named {assetName}.";
        }
        else
        {
            var zips = assets
                .Where(a => a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (zips.Count == 1)
                asset = zips[0];
            else
                problem =
                    zips.Count == 0
                        ? "The release has no .zip asset: attach the plugin package to it."
                        : $"The release has {zips.Count} .zip assets: name the one to install.";
        }

        return new GitHubRelease(
            root.GetProperty("tag_name").GetString() ?? "",
            root.TryGetProperty("html_url", out var url) ? url.GetString() ?? "" : "",
            root.TryGetProperty("published_at", out var at) && at.ValueKind == JsonValueKind.String
                ? at.GetDateTime().ToUniversalTime()
                : null,
            asset,
            problem
        );
    }

    /// <summary>
    /// The asset's bytes, no more than <paramref name="limit"/>, checked against the SHA-256
    /// GitHub gives for it (when it gives one).
    /// </summary>
    public async Task<MemoryStream> DownloadAsync(
        GitHubAsset asset,
        string? token,
        long limit,
        CancellationToken ct
    )
    {
        var tooLarge = $"{asset.Name} is larger than {limit / (1024 * 1024)} MB.";

        if (asset.Size > limit)
            throw new GitHubException(tooLarge);

        using var request = Request(asset.ApiUrl, token, "application/octet-stream");
        using var response = await http.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                ct
            )
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
            throw new GitHubException(
                $"GitHub did not give {asset.Name} ({(int)response.StatusCode} {response.ReasonPhrase})."
            );

        var buffer = new MemoryStream();
        var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);

        await using (stream.ConfigureAwait(false))
        {
            var chunk = new byte[81920];
            int read;

            while ((read = await stream.ReadAsync(chunk, ct).ConfigureAwait(false)) > 0)
            {
                if (buffer.Length + read > limit)
                    throw new GitHubException(tooLarge);

                await buffer.WriteAsync(chunk.AsMemory(0, read), ct).ConfigureAwait(false);
            }
        }

        if (asset.Sha256 is { } expected)
        {
            var actual = Convert.ToHexStringLower(SHA256.HashData(buffer.ToArray()));

            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                throw new GitHubException(
                    $"{asset.Name} is not the file GitHub lists: its SHA-256 differs. Nothing was installed."
                );
        }

        buffer.Position = 0;

        return buffer;
    }

    private static HttpRequestMessage Request(string url, string? token, string accept)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);

        request.Headers.UserAgent.ParseAdd(USER_AGENT);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(accept));
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");

        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return request;
    }

    private static GitHubAsset ReadAsset(JsonElement asset)
    {
        var digest =
            asset.TryGetProperty("digest", out var d) && d.ValueKind == JsonValueKind.String
                ? d.GetString()
                : null;

        return new GitHubAsset(
            asset.GetProperty("name").GetString() ?? "",
            asset.GetProperty("url").GetString() ?? "",
            asset.TryGetProperty("size", out var size) ? size.GetInt64() : 0,
            digest is not null && digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)
                ? digest[7..]
                : null
        );
    }

    private static string Refusal(
        HttpResponseMessage response,
        string repository,
        string? tag,
        bool hasToken
    ) =>
        response.StatusCode switch
        {
            HttpStatusCode.NotFound when tag is not null =>
                $"{repository} has no release tagged {tag}.",
            HttpStatusCode.NotFound => hasToken
                ? $"{repository} has no release, or the token can't see it."
                : $"{repository} has no release, or it is private: add a token that can read it.",
            HttpStatusCode.Unauthorized => "GitHub refused the token: it is wrong or expired.",
            HttpStatusCode.Forbidden
            or HttpStatusCode.TooManyRequests
                when response.Headers.TryGetValues("x-ratelimit-remaining", out var left)
                    && left.FirstOrDefault() == "0" =>
                "GitHub's rate limit is used up; the next check tries again. A token raises the limit.",
            HttpStatusCode.Forbidden => "GitHub refused: the token can't read the repository.",
            _ => $"GitHub answered {(int)response.StatusCode} {response.ReasonPhrase}.",
        };

    [GeneratedRegex(@"^(https?://)?(www\.)?github\.com/", RegexOptions.IgnoreCase)]
    private static partial Regex RepositoryPrefix();

    [GeneratedRegex(@"^[A-Za-z0-9_.-]{1,100}$")]
    private static partial Regex NamePattern();
}
