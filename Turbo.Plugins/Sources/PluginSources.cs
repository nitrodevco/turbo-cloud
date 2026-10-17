using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.Plugins.Configuration;
using Turbo.Primitives.Plugins;

namespace Turbo.Plugins.Sources;

/// <summary>
/// <see cref="IPluginSources"/>: links kept in <c>plugins.json</c> (<see cref="PluginStateFile"/>),
/// GitHub asked through <see cref="GitHubReleases"/>, and every check or update a
/// <see cref="PluginJobs"/> job. An update installs through
/// <see cref="PluginAdministration.InstallAsync(System.IO.Stream, string?, CancellationToken)"/>,
/// the same checks as an uploaded package, so a release is kept to roll back to and put back when
/// its successor does not load.
/// </summary>
internal sealed class PluginSources(
    PluginManager manager,
    PluginAdministration plugins,
    PluginJobs jobs,
    PluginSecrets secrets,
    GitHubReleases github,
    IOptions<PluginConfig> config,
    TimeProvider time,
    ILogger<PluginSources> logger
) : IPluginSources
{
    /// <summary>The steps of an update: ask GitHub, download, install.</summary>
    private const int UPDATE_STEPS = 3;

    private PluginStateFile State => manager.State;

    public ImmutableArray<string> AllowedOwners =>
        [
            .. config
                .Value.AllowedSources.Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim()),
        ];

    public int CheckMinutes => Math.Max(0, config.Value.UpdateCheckMinutes);

    public PluginJobSnapshot? Job => jobs.Current;

    public ImmutableArray<PluginSourceSnapshot> List() => [.. State.Sources.Select(Snapshot)];

    public PluginSourceResult Add(PluginSourceInput input, int player)
    {
        if (!plugins.UploadEnabled)
            return Refuse("Installing plugins from the panel is off.");

        if (Validate(input, out var repository) is { } refusal)
            return Refuse(refusal);

        var token = string.IsNullOrEmpty(input.Token) ? null : secrets.Seal(input.Token.Trim());
        var added = State.EditSources(sources =>
        {
            if (
                sources.Any(x =>
                    string.Equals(x.Repository, repository, StringComparison.OrdinalIgnoreCase)
                )
            )
                return null;

            var entry = new PluginSourceEntry
            {
                Id = Guid.NewGuid().ToString("N")[..12],
                Repository = repository,
                Follow = FollowOf(input.Follow),
                Tag = FollowOf(input.Follow) == PluginSourceFollow.TAG ? input.Tag?.Trim() : null,
                Asset = Blank(input.Asset),
                Token = token,
                AutoUpdate = input.AutoUpdate,
            };

            sources.Add(entry);

            return entry;
        });

        if (added is null)
            return Refuse($"{repository} is linked already.");

        logger.LogWarning("Plugin source {Repository} linked from the admin panel", repository);

        return StartUpdate(added, player, $"Linked {repository}; installing its release.") with
        {
            Source = Snapshot(added),
        };
    }

    public PluginSourceResult? Edit(string id, PluginSourceInput input)
    {
        if (Find(id) is not { } current)
            return null;

        if (Validate(input with { Repository = current.Repository }, out _) is { } refusal)
            return Refuse(refusal);

        var token = input.Token switch
        {
            null => current.Token,
            "" => null,
            _ => secrets.Seal(input.Token.Trim()),
        };
        var edited = Save(
            id,
            x =>
                x with
                {
                    Follow = FollowOf(input.Follow),
                    Tag =
                        FollowOf(input.Follow) == PluginSourceFollow.TAG ? input.Tag?.Trim() : null,
                    Asset = Blank(input.Asset),
                    Token = token,
                    AutoUpdate = input.AutoUpdate,
                    // What GitHub offers depends on what is followed: check again.
                    LatestTag = null,
                    LatestUrl = null,
                    LatestPublishedAtUtc = null,
                    CheckedAtUtc = null,
                    Error = null,
                }
        );

        return edited is null
            ? null
            : new PluginSourceResult(true, $"Saved {edited.Repository}.", Snapshot(edited), null);
    }

    public PluginSourceResult? Remove(string id)
    {
        var removed = State.EditSources(sources =>
        {
            var entry = sources.FirstOrDefault(x => x.Id == id);

            if (entry is not null)
                sources.Remove(entry);

            return entry;
        });

        return removed is null
            ? null
            : new PluginSourceResult(
                true,
                removed.Key is null
                    ? $"Unlinked {removed.Repository}."
                    : $"Unlinked {removed.Repository}; {removed.Key} stays installed.",
                Snapshot(removed),
                null
            );
    }

    public PluginSourceResult? Check(string? id, int player)
    {
        var targets =
            id is null ? [.. State.Sources]
            : Find(id) is { } one ? [one]
            : (List<PluginSourceEntry>)[];

        if (targets.Count == 0)
            return id is null ? Refuse("No repository is linked.") : null;

        var title =
            targets.Count == 1 ? $"Check {targets[0].Repository}" : "Check the linked repositories";

        return Start(
            () =>
                jobs.Start(
                    PluginJobKind.Check,
                    title,
                    targets.Count,
                    player,
                    async (progress, ct) =>
                    {
                        var found = 0;

                        foreach (var source in targets)
                        {
                            progress.Step($"Asking GitHub about {source.Repository}");

                            var checkedSource = await CheckOneAsync(source.Id, ct)
                                .ConfigureAwait(false);

                            if (checkedSource is null)
                                continue;

                            progress.Log(Describe(checkedSource));

                            if (checkedSource.Error is null && UpdateAvailable(checkedSource))
                                found++;
                        }

                        return found switch
                        {
                            0 => "No update.",
                            1 => "1 update available.",
                            _ => $"{found} updates available.",
                        };
                    }
                ),
            "Checking."
        );
    }

    public PluginSourceResult? Update(string id, int player)
    {
        if (!plugins.UploadEnabled)
            return Refuse("Installing plugins from the panel is off.");

        return Find(id) is { } source
            ? StartUpdate(source, player, $"Updating {source.Repository}.")
            : null;
    }

    public bool CancelJob() => jobs.Cancel();

    /// <summary>
    /// The scheduled check: every link asked in turn, outside the jobs (it would hide the last
    /// one staff ran). A link with automatic updates and a new release starts its update.
    /// </summary>
    public async Task CheckAllAsync(CancellationToken ct)
    {
        foreach (var source in State.Sources)
        {
            var result = await CheckOneAsync(source.Id, ct).ConfigureAwait(false);

            if (result is null)
                continue;

            if (result.Error is not null)
            {
                logger.LogWarning(
                    "Plugin source {Repository} could not be checked: {Error}",
                    result.Repository,
                    result.Error
                );

                continue;
            }

            if (!UpdateAvailable(result))
                continue;

            logger.LogInformation(
                "Plugin source {Repository} has {Tag} (installed {Installed})",
                result.Repository,
                result.LatestTag,
                result.InstalledTag ?? "nothing"
            );

            if (!result.AutoUpdate || result.LatestTag == result.SkipTag || !plugins.UploadEnabled)
                continue;

            if (jobs.Running)
            {
                logger.LogInformation(
                    "Plugin source {Repository}: a job is running; the update waits for the next check",
                    result.Repository
                );

                continue;
            }

            // The timer is no player's: an update it starts is the server's own (player 0).
            StartUpdate(result, 0, "");
        }
    }

    private async Task<PluginSourceEntry?> CheckOneAsync(string id, CancellationToken ct)
    {
        if (Find(id) is not { } source)
            return null;

        try
        {
            var release = await github
                .GetReleaseAsync(
                    source.Repository,
                    TagOf(source),
                    source.Asset,
                    TokenOf(source),
                    ct
                )
                .ConfigureAwait(false);

            return Save(
                id,
                x =>
                    x with
                    {
                        LatestTag = release.Tag,
                        LatestUrl = release.HtmlUrl,
                        LatestPublishedAtUtc = release.PublishedAtUtc,
                        CheckedAtUtc = time.GetUtcNow().UtcDateTime,
                        Error = release.AssetProblem,
                    }
            );
        }
        catch (Exception ex)
            when (ex
                    is GitHubException
                        or InvalidOperationException
                        or System.Net.Http.HttpRequestException
                || (ex is OperationCanceledException && !ct.IsCancellationRequested)
            )
        {
            return Save(
                id,
                x =>
                    x with
                    {
                        CheckedAtUtc = time.GetUtcNow().UtcDateTime,
                        Error = ex
                            is System.Net.Http.HttpRequestException
                                or OperationCanceledException
                            ? $"GitHub could not be reached: {ex.Message}"
                            : ex.Message,
                    }
            );
        }
    }

    private PluginSourceResult StartUpdate(PluginSourceEntry source, int player, string message) =>
        Start(
            () =>
                jobs.Start(
                    PluginJobKind.Update,
                    $"Update from {source.Repository}",
                    UPDATE_STEPS,
                    player,
                    (progress, ct) => UpdateAsync(source.Id, progress, ct)
                ),
            message
        );

    private async Task<string> UpdateAsync(
        string id,
        IPluginJobProgress progress,
        CancellationToken ct
    )
    {
        var source = Find(id) ?? throw new PluginJobFailure("The link was removed.");

        progress.Step(
            $"Asking GitHub for {(TagOf(source) is { } tag ? $"the release {tag}" : "the newest release")} of {source.Repository}"
        );

        GitHubRelease release;

        try
        {
            release = await github
                .GetReleaseAsync(
                    source.Repository,
                    TagOf(source),
                    source.Asset,
                    TokenOf(source),
                    ct
                )
                .ConfigureAwait(false);
        }
        catch (GitHubException ex)
        {
            Save(
                id,
                x => x with { Error = ex.Message, CheckedAtUtc = time.GetUtcNow().UtcDateTime }
            );

            throw;
        }

        Save(
            id,
            x =>
                x with
                {
                    LatestTag = release.Tag,
                    LatestUrl = release.HtmlUrl,
                    LatestPublishedAtUtc = release.PublishedAtUtc,
                    CheckedAtUtc = time.GetUtcNow().UtcDateTime,
                    Error = release.AssetProblem,
                }
        );

        if (release.Asset is not { } asset)
            throw new PluginJobFailure(release.AssetProblem ?? "The release has no package.");

        progress.Log(
            $"Release {release.Tag}: {asset.Name}, {asset.Size:N0} bytes{(asset.Sha256 is null ? "" : ", SHA-256 listed")}."
        );
        progress.Step($"Downloading {asset.Name}");

        using var package = await github
            .DownloadAsync(asset, TokenOf(source), plugins.MaxUploadBytes, ct)
            .ConfigureAwait(false);

        progress.Log(asset.Sha256 is null ? "Downloaded." : "Downloaded; its SHA-256 matches.");
        progress.Step("Installing");

        // From here on the plugin folder changes: not stopped halfway.
        var result = await plugins
            .InstallAsync(package, source.Key, CancellationToken.None)
            .ConfigureAwait(false);

        if (!result.Ok)
        {
            Save(id, x => x with { Error = result.Message });

            throw new PluginJobFailure(result.Message);
        }

        Save(
            id,
            x =>
                x with
                {
                    Key = result.Plugin?.Key ?? x.Key,
                    InstalledTag = release.Tag,
                    InstalledVersion = result.Plugin?.Version,
                    SkipTag = null,
                    Error = null,
                }
        );

        logger.LogWarning(
            "Plugin {Key}@{Version} installed from {Repository} {Tag}",
            result.Plugin?.Key,
            result.Plugin?.Version,
            source.Repository,
            release.Tag
        );

        return $"{result.Message} ({source.Repository} {release.Tag})";
    }

    private PluginSourceResult Start(Func<PluginJobSnapshot> start, string message)
    {
        try
        {
            return new PluginSourceResult(true, message, null, start());
        }
        catch (InvalidOperationException ex)
        {
            return Refuse(ex.Message);
        }
    }

    private string? Validate(PluginSourceInput input, out string repository)
    {
        repository = GitHubReleases.ParseRepository(input.Repository) ?? "";

        if (repository.Length == 0)
            return "Enter the repository as https://github.com/owner/repo or owner/repo.";

        var owner = repository.Split('/')[0];
        var allowed = AllowedOwners;

        if (allowed.Length > 0 && !allowed.Contains(owner, StringComparer.OrdinalIgnoreCase))
            return $"Plugins can't be linked from {owner}: the server allows {string.Join(", ", allowed)} (Turbo:Plugin:AllowedSources).";

        var follow = input.Follow?.Trim().ToLowerInvariant();

        if (follow is not (null or "" or PluginSourceFollow.LATEST or PluginSourceFollow.TAG))
            return "Follow the newest release (latest) or one tag (tag).";

        if (follow == PluginSourceFollow.TAG && string.IsNullOrWhiteSpace(input.Tag))
            return "Enter the tag to follow.";

        return null;
    }

    private PluginSourceEntry? Save(string id, Func<PluginSourceEntry, PluginSourceEntry> change) =>
        State.EditSources(sources =>
        {
            var index = sources.FindIndex(x => x.Id == id);

            if (index < 0)
                return null;

            sources[index] = change(sources[index]);

            return sources[index];
        });

    private PluginSourceEntry? Find(string id) => State.Sources.FirstOrDefault(x => x.Id == id);

    private string? TokenOf(PluginSourceEntry source) =>
        source.Token is null ? null : secrets.Unseal(source.Token);

    private static string? TagOf(PluginSourceEntry source) =>
        source.Follow == PluginSourceFollow.TAG ? source.Tag : null;

    private static bool UpdateAvailable(PluginSourceEntry source) =>
        source.LatestTag is not null && source.LatestTag != source.InstalledTag;

    private static string FollowOf(string? follow) =>
        string.Equals(follow?.Trim(), PluginSourceFollow.TAG, StringComparison.OrdinalIgnoreCase)
            ? PluginSourceFollow.TAG
            : PluginSourceFollow.LATEST;

    private static string? Blank(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static string Describe(PluginSourceEntry source) =>
        source.Error is { } error ? $"{source.Repository}: {error}"
        : UpdateAvailable(source)
            ? $"{source.Repository}: {source.LatestTag} is available (installed: {source.InstalledTag ?? "nothing"})."
        : $"{source.Repository}: {source.InstalledTag} is the newest.";

    private static PluginSourceResult Refuse(string message) => new(false, message, null, null);

    private static PluginSourceSnapshot Snapshot(PluginSourceEntry x) =>
        new(
            x.Id,
            x.Repository,
            x.Follow,
            x.Tag,
            x.Asset,
            x.Token is not null,
            x.AutoUpdate,
            x.Key,
            x.InstalledTag,
            x.InstalledVersion,
            x.LatestTag,
            x.LatestUrl,
            x.LatestPublishedAtUtc,
            x.CheckedAtUtc,
            x.Error,
            UpdateAvailable(x)
        );
}
