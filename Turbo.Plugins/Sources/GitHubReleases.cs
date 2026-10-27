using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Turbo.Plugins.Configuration;

namespace Turbo.Plugins.Sources;

/// <summary>A release on GitHub: its tag is what is installed, from the tag's sources.</summary>
internal sealed record GitHubRelease(string Tag, string HtmlUrl, DateTime? PublishedAtUtc);

/// <summary>Why GitHub did not give what was asked, in words for the panel.</summary>
internal sealed class GitHubException(string message) : Exception(message);

/// <summary>
/// GitHub's releases REST API (<see cref="PluginConfig.GitHubApiUrl"/>): a repository's newest
/// release or one by tag, and the tag's source archive. A plugin is installed as its sources,
/// which the server compiles, never as a built asset. A token, when given, is sent to GitHub only:
/// .NET drops it on the redirect to the archive's host.
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

    /// <summary>The newest release (<paramref name="tag"/> null) or the release tagged <paramref name="tag"/>.</summary>
    public async Task<GitHubRelease> GetReleaseAsync(
        string repository,
        string? tag,
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

        return new GitHubRelease(
            root.GetProperty("tag_name").GetString() ?? "",
            root.TryGetProperty("html_url", out var url) ? url.GetString() ?? "" : "",
            root.TryGetProperty("published_at", out var at) && at.ValueKind == JsonValueKind.String
                ? at.GetDateTime().ToUniversalTime()
                : null
        );
    }

    /// <summary>
    /// The repository's files at <paramref name="tag"/>, as GitHub zips them (every file under
    /// one top folder), no more than <paramref name="limit"/> bytes.
    /// </summary>
    public async Task<MemoryStream> DownloadSourcesAsync(
        string repository,
        string tag,
        string? token,
        long limit,
        CancellationToken ct
    )
    {
        var tooLarge = $"The sources of {tag} are larger than {limit / (1024 * 1024)} MB.";

        using var request = Request(
            $"{ApiUrl}/repos/{repository}/zipball/{Uri.EscapeDataString(tag)}",
            token,
            "application/vnd.github+json"
        );
        using var response = await http.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                ct
            )
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
            throw new GitHubException(
                $"GitHub did not give the sources of {tag} ({(int)response.StatusCode} {response.ReasonPhrase})."
            );

        if (response.Content.Headers.ContentLength > limit)
            throw new GitHubException(tooLarge);

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
