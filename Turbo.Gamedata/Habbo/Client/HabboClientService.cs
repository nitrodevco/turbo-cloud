using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.Assets;
using Turbo.Assets.Asar;
using Turbo.Assets.Swf;
using Turbo.Gamedata.Configuration;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Gamedata.Snapshots;

namespace Turbo.Gamedata.Habbo.Client;

/// <summary>
/// <see cref="IHabboClientService"/>, as nitro-studio's <c>server/habbo-client.ts</c> takes the
/// client: each environment's <c>https://&lt;host&gt;/gamedata/clienturls</c> gives
/// <c>html-windows-version</c> (the release) and <c>html-windows</c> - a <c>native-release.json</c>
/// listing naming the <c>app.asar</c> beside it with its size and SHA-256, or (older releases) a
/// <c>.zip</c> holding <c>resources/app.asar</c>. A release is downloaded once whichever
/// environment served it, checked against the listing and against every file its own
/// <c>release-manifest.json</c> lists, and kept under the bundle folder
/// (<c>.habbo-client/releases/&lt;version&gt;/</c>) with a <c>release.json</c>; the newest
/// <see cref="GamedataConfig.ClientKeepReleases"/>, and every one an environment is on, are kept.
/// <para>
/// The same clienturls name Habbo's AIR client: <c>flash-windows-version</c> and
/// <c>flash-windows</c>, a <c>HabboWin.zip</c> holding <c>HabboAir.swf</c>. Its newest release is
/// downloaded when a build first asks for it (<see cref="ReadAirAsync"/>), and only its
/// <c>HabboAir.swf</c> kept (<c>.habbo-client/air/&lt;version&gt;/</c>), the newest only.
/// </para>
/// </summary>
internal sealed partial class HabboClientService(
    IHttpClientFactory httpClients,
    IAssetBundleStore store,
    IOptions<GamedataConfig> config,
    TimeProvider time,
    ILogger<HabboClientService> logger
) : IHabboClientService
{
    public const string HTTP_CLIENT = "habbo-client";

    private const string FOLDER = ".habbo-client";
    private const string ENVIRONMENTS_FILE = "environments.json";
    private const string RELEASE_FILE = "release.json";
    private const string ASAR_FILE = "app.asar";
    private const string MANIFEST = "release-manifest.json";
    private const string BUILD_INFO = "habbo-air/launcher-build-info.json";
    private const string ZIP_ASAR = "resources/app.asar";
    private const long MAX_ASAR = 512L * 1024 * 1024;
    private const string AIR_FOLDER = "air";
    private const string AIR_SWF = "HabboAir.swf";

    private static readonly JsonSerializerOptions JSON = new() { WriteIndented = true };

    /// <summary>Habbo's environments, in the order they are asked and followed.</summary>
    public static readonly ImmutableArray<(string Name, string Host)> ENVIRONMENTS =
    [
        ("production", "www.habbo.com"),
        ("sandbox", "sandbox.habbo.com"),
    ];

    private readonly ConcurrentDictionary<string, Task> _downloading = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> _downloadErrors = new(
        StringComparer.Ordinal
    );
    private readonly SemaphoreSlim _state = new(1, 1);

    /// <summary>A release downloaded, checked and kept, by version: raised once its environments are on it.</summary>
    public event Action<string>? ReleaseDownloaded;

    private string Root => Path.Combine(store.Root, FOLDER);

    private string ReleasesRoot => Path.Combine(Root, "releases");

    public async Task<HabboClientStatus> GetStatusAsync(CancellationToken ct)
    {
        var environments = await ReadEnvironmentsAsync(ct).ConfigureAwait(false);
        var releases = ReadReleases();
        var followed = Follow(environments);

        return new HabboClientStatus
        {
            Environments =
            [
                .. ENVIRONMENTS.Select(x =>
                {
                    var state = environments.GetValueOrDefault(x.Name) ?? new EnvironmentState();

                    return new HabboClientEnvironmentSnapshot
                    {
                        Environment = x.Name,
                        Host = x.Host,
                        CheckedAt = state.CheckedAt,
                        LiveVersion = state.LiveVersion,
                        Error = state.Error,
                        Version = state.Version,
                        Downloading =
                            state.LiveVersion is { } live && _downloading.ContainsKey(live),
                        DownloadError = state.LiveVersion is { } failed
                            ? _downloadErrors.GetValueOrDefault(failed)
                            : null,
                    };
                }),
            ],
            Releases = [.. releases.Select(x => x.Snapshot)],
            Follow = followed,
            Followed = followed is null ? null : environments[followed].Version,
        };
    }

    public async Task<HabboClientStatus> CheckAsync(bool wait, CancellationToken ct)
    {
        var started = new List<Task>();

        foreach (var (name, host) in ENVIRONMENTS)
        {
            var (version, url, air, error) = await AskAsync(host, ct).ConfigureAwait(false);

            await UpdateEnvironmentAsync(
                    name,
                    state =>
                        error is null
                            ? state with
                            {
                                CheckedAt = time.GetUtcNow().UtcDateTime,
                                LiveVersion = version,
                                LiveUrl = url,
                                AirVersion = air?.Version,
                                AirUrl = air?.Url,
                                Error = null,
                            }
                            : state with
                            {
                                CheckedAt = time.GetUtcNow().UtcDateTime,
                                Error = error,
                            },
                    ct
                )
                .ConfigureAwait(false);

            if (error is not null || version is null || url is null)
                continue;

            if (ReadRelease(version) is not null)
                await AdoptAsync(name, version, ct).ConfigureAwait(false);
            else
                started.Add(StartDownloadAsync(name, version, url));
        }

        if (wait)
        {
            foreach (var download in started)
            {
                try
                {
                    await download.WaitAsync(ct).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Said in the status; the next check tries again.
                }
            }
        }

        return await GetStatusAsync(ct).ConfigureAwait(false);
    }

    public async Task<bool> ForgetAsync(string version, CancellationToken ct)
    {
        if (
            !IsVersion(version)
            || _downloading.ContainsKey(version)
            || ReadRelease(version) is null
        )
            return false;

        Directory.Delete(Path.Combine(ReleasesRoot, version), recursive: true);

        foreach (var (name, _) in ENVIRONMENTS)
            await UpdateEnvironmentAsync(
                    name,
                    x => x.Version == version ? x with { Version = null } : x,
                    ct
                )
                .ConfigureAwait(false);

        logger.LogInformation("Habbo client release {Version} forgotten", version);

        return true;
    }

    /// <summary>The release the hotel builds from - the followed environment's - opened; null when none is downloaded.</summary>
    public async Task<HabboClientRelease?> OpenFollowedAsync(CancellationToken ct)
    {
        var environments = await ReadEnvironmentsAsync(ct).ConfigureAwait(false);

        if (
            Follow(environments) is not { } followed
            || environments[followed].Version is not { } version
        )
            return null;

        var release = ReadRelease(version);

        if (release is null)
            return null;

        var asar = AsarArchive.Open(Path.Combine(ReleasesRoot, version, ASAR_FILE));

        return new HabboClientRelease(
            version,
            followed,
            asar,
            RootOf(asar),
            release.Files.ToImmutableDictionary(x => x.Path, x => x.Sha256, StringComparer.Ordinal)
        );
    }

    /// <summary>
    /// The environment the hotel builds from: <see cref="GamedataConfig.ClientEnvironment"/>, or with
    /// <c>auto</c> the first (production, then sandbox) that has a release downloaded.
    /// </summary>
    private string? Follow(IReadOnlyDictionary<string, EnvironmentState> environments)
    {
        var wanted = config.Value.ClientEnvironment.Trim().ToLowerInvariant();

        if (ENVIRONMENTS.Any(x => x.Name == wanted))
            return environments.GetValueOrDefault(wanted)?.Version is null ? null : wanted;

        return ENVIRONMENTS
            .Select(x => x.Name)
            .FirstOrDefault(x => environments.GetValueOrDefault(x)?.Version is not null);
    }

    /// <summary>
    /// What an environment's clienturls name: the release and its address, the AIR client's, or why
    /// it could not be asked.
    /// </summary>
    private async Task<(
        string? Version,
        string? Url,
        (string Version, string Url)? Air,
        string? Error
    )> AskAsync(string host, CancellationToken ct)
    {
        var url = $"https://{host}/gamedata/clienturls";

        try
        {
            var http = httpClients.CreateClient(HabboGamedataClient.HTTP_CLIENT);
            using var response = await http.GetAsync(url, ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                return (null, null, null, $"{(int)response.StatusCode}: {url}");

            var text = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (JsonNode.Parse(text) is not JsonObject urls)
                return (null, null, null, "clienturls is not an object.");

            string? Read(string key) =>
                urls[key] is JsonValue value && value.TryGetValue<string>(out var s) && s.Length > 0
                    ? s
                    : null;

            var version = Read("html-windows-version");
            var address = Read("html-windows");
            (string Version, string Url)? air =
                Read("flash-windows-version") is { } airVersion
                && IsVersion(airVersion)
                && Read("flash-windows") is { } airAddress
                    ? (airVersion, airAddress)
                    : null;

            // A version and no address is no client (production's "0").
            if (version is null || address is null)
                return (null, null, air, null);

            return IsVersion(version)
                ? (version, address, air, null)
                : (
                    null,
                    null,
                    air,
                    $"clienturls names a release this server won't keep: {version}"
                );
        }
        catch (Exception ex)
            when (ex is HttpRequestException or JsonException or TaskCanceledException
                && !ct.IsCancellationRequested
            )
        {
            return (null, null, null, ex.Message);
        }
    }

    /// <summary>
    /// The newest release of Habbo's AIR client the environments named when last asked (the highest
    /// <c>flash-windows-version</c>), and its address; null when none named one.
    /// </summary>
    public async Task<(string Version, string Url)?> NewestAirAsync(CancellationToken ct)
    {
        var environments = await ReadEnvironmentsAsync(ct).ConfigureAwait(false);

        return environments
            .Values.Where(x => x.AirVersion is { } v && IsVersion(v) && x.AirUrl is not null)
            .OrderByDescending(x => long.TryParse(x.AirVersion, out var n) ? n : -1)
            .ThenByDescending(x => x.AirVersion, StringComparer.Ordinal)
            .Select(x => ((string Version, string Url)?)(x.AirVersion!, x.AirUrl!))
            .FirstOrDefault();
    }

    /// <summary>
    /// Habbo's AIR client's <c>HabboAir.swf</c> of the release: the one kept, else downloaded from
    /// its <c>HabboWin.zip</c> and kept in place of the one before.
    /// </summary>
    public async Task<byte[]> ReadAirAsync(string version, string url, CancellationToken ct)
    {
        if (!IsVersion(version))
            throw new InvalidDataException(
                $"Habbo's AIR client {version} is not a release this server keeps."
            );

        var folder = Path.Combine(Root, AIR_FOLDER);
        var path = Path.Combine(folder, version, AIR_SWF);

        if (File.Exists(path))
            return await File.ReadAllBytesAsync(path, ct).ConfigureAwait(false);

        var partial = Path.Combine(folder, $"{version}.{Guid.NewGuid():N}.partial");

        Directory.CreateDirectory(partial);

        try
        {
            logger.LogInformation(
                "Downloading Habbo's AIR client {Version} from {Url}",
                version,
                url
            );

            var zipPath = Path.Combine(partial, "client.zip");

            await DownloadToAsync(url, zipPath, null, null, ct).ConfigureAwait(false);

            var zip = await ZipFile.OpenReadAsync(zipPath, ct).ConfigureAwait(false);

            await using (zip.ConfigureAwait(false))
            {
                var entry =
                    zip.Entries.FirstOrDefault(x => x.FullName.Replace('\\', '/') == AIR_SWF)
                    ?? throw new InvalidDataException(
                        $"Habbo's AIR client {version} has no {AIR_SWF}."
                    );

                if (entry.Length > AssetLimits.MAX_INFLATED_BYTES)
                    throw new InvalidDataException(
                        $"Habbo's AIR client {version} has a {AIR_SWF} larger than this server takes."
                    );

                await entry
                    .ExtractToFileAsync(Path.Combine(partial, AIR_SWF), ct)
                    .ConfigureAwait(false);
            }

            File.Delete(zipPath);

            var data = await File.ReadAllBytesAsync(Path.Combine(partial, AIR_SWF), ct)
                .ConfigureAwait(false);

            if (!SwfLibrary.IsSwf(data))
                throw new InvalidDataException(
                    $"Habbo's AIR client {version} has a {AIR_SWF} that is not an SWF."
                );

            // Only the newest is kept.
            foreach (var before in Directory.EnumerateDirectories(folder))
                if (!before.EndsWith(".partial", StringComparison.Ordinal))
                    Directory.Delete(before, recursive: true);

            Directory.Move(partial, Path.Combine(folder, version));
            logger.LogInformation(
                "Habbo's AIR client {Version} downloaded: {Bytes} bytes of {File}",
                version,
                data.Length,
                AIR_SWF
            );

            return data;
        }
        catch
        {
            if (Directory.Exists(partial))
                Directory.Delete(partial, recursive: true);

            throw;
        }
    }

    private Task StartDownloadAsync(string environment, string version, string url) =>
        _downloading.GetOrAdd(
            version,
            v =>
                Task.Run(async () =>
                {
                    try
                    {
                        await DownloadAsync(v, url, CancellationToken.None).ConfigureAwait(false);
                        _downloadErrors.TryRemove(v, out _);
                        await AdoptAsync(environment, v, CancellationToken.None)
                            .ConfigureAwait(false);

                        // Any other environment on the same release has it too.
                        foreach (var (name, _) in ENVIRONMENTS)
                        {
                            var state = (
                                await ReadEnvironmentsAsync(CancellationToken.None)
                                    .ConfigureAwait(false)
                            ).GetValueOrDefault(name);

                            if (name != environment && state?.LiveVersion == v)
                                await AdoptAsync(name, v, CancellationToken.None)
                                    .ConfigureAwait(false);
                        }

                        await PruneAsync(CancellationToken.None).ConfigureAwait(false);
                        ReleaseDownloaded?.Invoke(v);
                    }
                    catch (Exception ex)
                    {
                        _downloadErrors[v] = ex.Message;
                        logger.LogWarning(
                            ex,
                            "Habbo client release {Version} could not be downloaded from {Url}",
                            v,
                            url
                        );

                        throw;
                    }
                    finally
                    {
                        _downloading.TryRemove(v, out _);
                    }
                })
        );

    /// <summary>Downloads and checks a release into a folder of its own, beside its place until it is whole.</summary>
    private async Task DownloadAsync(string version, string url, CancellationToken ct)
    {
        var final = Path.Combine(ReleasesRoot, version);
        var partial = final + ".partial";

        if (Directory.Exists(partial))
            Directory.Delete(partial, recursive: true);

        Directory.CreateDirectory(partial);

        try
        {
            logger.LogInformation(
                "Downloading Habbo client release {Version} from {Url}",
                version,
                url
            );

            var asarPath = Path.Combine(partial, ASAR_FILE);
            string source;

            if (new Uri(url).AbsolutePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                var listing = await ListingAsync(url, ct).ConfigureAwait(false);

                source = new Uri(new Uri(url), listing.Archive).ToString();
                await DownloadToAsync(source, asarPath, listing.Size, listing.Sha256, ct)
                    .ConfigureAwait(false);
            }
            else
            {
                // An older release: a zip of the whole client, the asar inside it.
                source = url;

                var zipPath = Path.Combine(partial, "client.zip");

                await DownloadToAsync(url, zipPath, null, null, ct).ConfigureAwait(false);

                var zip = await ZipFile.OpenReadAsync(zipPath, ct).ConfigureAwait(false);

                await using (zip.ConfigureAwait(false))
                {
                    var entry =
                        zip.Entries.FirstOrDefault(x => x.FullName.Replace('\\', '/') == ZIP_ASAR)
                        ?? throw new InvalidDataException($"The release's zip has no {ZIP_ASAR}.");

                    await entry.ExtractToFileAsync(asarPath, ct).ConfigureAwait(false);
                }

                File.Delete(zipPath);
            }

            var asar = AsarArchive.Open(asarPath);
            var root = RootOf(asar);
            var files = Verify(asar, root, out var releaseId);
            var built = BuiltAt(asar, root);
            var release = new ReleaseFile
            {
                Version = version,
                ReleaseId = releaseId ?? version,
                Url = source,
                DownloadedAt = time.GetUtcNow().UtcDateTime,
                BuiltAt = built,
                Environments = [],
                Files = files,
                Size = new FileInfo(asarPath).Length,
            };

            await File.WriteAllTextAsync(
                    Path.Combine(partial, RELEASE_FILE),
                    JsonSerializer.Serialize(release, JSON),
                    ct
                )
                .ConfigureAwait(false);

            if (Directory.Exists(final))
                Directory.Delete(final, recursive: true);

            Directory.Move(partial, final);
            logger.LogInformation(
                "Habbo client release {Version} downloaded: {Files} files checked",
                version,
                files.Count
            );
        }
        catch
        {
            if (Directory.Exists(partial))
                Directory.Delete(partial, recursive: true);

            throw;
        }
    }

    private async Task<(string Archive, long Size, string Sha256)> ListingAsync(
        string url,
        CancellationToken ct
    )
    {
        var http = httpClients.CreateClient(HabboGamedataClient.HTTP_CLIENT);
        var text = await http.GetStringAsync(url, ct).ConfigureAwait(false);
        JsonNode? listing;

        try
        {
            listing = JsonNode.Parse(text);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(
                text.TrimStart().StartsWith('<')
                    ? $"Habbo sent a web page instead of {url} - it may be refusing this server's address."
                    : $"{url} is not JSON.",
                ex
            );
        }

        var asar = listing?["clientAsar"] as JsonObject;
        var archive = asar?["archive"]?.GetValue<string>();
        var size = asar?["size"]?.GetValue<long>() ?? 0;
        var sha256 = asar?["sha256"]?.GetValue<string>()?.ToLowerInvariant();

        if (archive is null || !IsVersion(archive))
            throw new InvalidDataException($"{url} names no app.asar beside it.");

        if (size <= 0 || size > MAX_ASAR)
            throw new InvalidDataException(
                $"{url} gives the app.asar a size this server won't take ({size})."
            );

        if (sha256 is null || !Sha256Pattern().IsMatch(sha256))
            throw new InvalidDataException($"{url} gives the app.asar no SHA-256.");

        return (archive, size, sha256);
    }

    /// <summary>
    /// Streams a file to disk, hashing it as it comes; with a size and hash, the file must be those.
    /// Without, it must at least be a zip. A web page in its place says so.
    /// </summary>
    private async Task DownloadToAsync(
        string url,
        string path,
        long? size,
        string? sha256,
        CancellationToken ct
    )
    {
        var http = httpClients.CreateClient(HTTP_CLIENT);
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var received = 0L;
        var head = new byte[512];
        var headLength = 0;
        var source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);

        await using (source.ConfigureAwait(false))
        {
            var file = new FileStream(
                path,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                81920,
                useAsync: true
            );

            await using (file.ConfigureAwait(false))
            {
                var buffer = new byte[81920];
                int read;

                while ((read = await source.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
                {
                    received += read;

                    if (size is { } limit && received > limit)
                        throw new InvalidDataException($"{url} is larger than its listing says.");

                    if (headLength < head.Length)
                    {
                        var take = Math.Min(read, head.Length - headLength);

                        Array.Copy(buffer, 0, head, headLength, take);
                        headLength += take;
                    }

                    hash.AppendData(buffer, 0, read);
                    await file.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                }
            }
        }

        var html =
            response.Content.Headers.ContentType?.MediaType?.Contains(
                "html",
                StringComparison.OrdinalIgnoreCase
            ) == true
            || (headLength > 0 && head[0] == (byte)'<');

        if (html)
            throw new InvalidDataException(
                $"Habbo sent a web page instead of {url} - it may be refusing this server's address."
            );

        if (response.Content.Headers.ContentLength is { } length && length != received)
            throw new InvalidDataException($"{url} was cut short ({received} of {length} bytes).");

        if (
            size is { } expected
            && (received != expected || Convert.ToHexStringLower(hash.GetHashAndReset()) != sha256)
        )
            throw new InvalidDataException(
                $"{url} is not the file its listing names (its size or SHA-256 differs)."
            );

        if (size is null && (headLength < 2 || head[0] != (byte)'P' || head[1] != (byte)'K'))
            throw new InvalidDataException($"{url} is not a zip.");
    }

    /// <summary>The client's folder inside the asar: <c>client/</c> in a zip-era release, else its root.</summary>
    private static string RootOf(AsarArchive asar) =>
        asar.Has($"client/{MANIFEST}") ? "client/" : "";

    /// <summary>Every file the release's manifest lists, each as large and hashed as it says.</summary>
    private static List<ReleaseFileEntry> Verify(
        AsarArchive asar,
        string root,
        out string? releaseId
    )
    {
        var text =
            asar.ReadText(root + MANIFEST)
            ?? throw new InvalidDataException($"The release has no {MANIFEST}.");
        var manifest =
            JsonNode.Parse(text) as JsonObject
            ?? throw new InvalidDataException($"{MANIFEST} is not an object.");
        var files = new List<ReleaseFileEntry>();

        releaseId = manifest["releaseId"]?.GetValue<string>();

        foreach (var entry in (manifest["files"] as JsonArray ?? []).OfType<JsonObject>())
        {
            var path =
                entry["path"]?.GetValue<string>()
                ?? throw new InvalidDataException($"{MANIFEST} lists a file with no path.");
            var size = entry["size"]?.GetValue<long>() ?? -1;
            var sha256 = entry["sha256"]?.GetValue<string>() ?? "";
            var data =
                asar.Read(root + path)
                ?? throw new InvalidDataException($"The release lacks {path}.");

            if (
                data.LongLength != size
                || Convert.ToHexStringLower(SHA256.HashData(data)) != sha256.ToLowerInvariant()
            )
                throw new InvalidDataException(
                    $"{path} is not the file the release's manifest lists."
                );

            files.Add(new ReleaseFileEntry(path, size, sha256.ToLowerInvariant()));
        }

        return files;
    }

    private static DateTime? BuiltAt(AsarArchive asar, string root)
    {
        try
        {
            return
                asar.ReadText(root + BUILD_INFO) is { } text
                && JsonNode.Parse(text)?["builtAtMs"] is JsonValue value
                && value.TryGetValue<double>(out var ms)
                ? DateTime.UnixEpoch.AddMilliseconds(ms)
                : null;
        }
        catch (Exception ex)
            when (ex is JsonException or AssetFormatException or InvalidOperationException)
        {
            return null;
        }
    }

    private async Task AdoptAsync(string environment, string version, CancellationToken ct)
    {
        var release = ReadRelease(version);

        if (release is not null && !release.Environments.Contains(environment))
        {
            release = release with { Environments = [.. release.Environments, environment] };
            await File.WriteAllTextAsync(
                    Path.Combine(ReleasesRoot, version, RELEASE_FILE),
                    JsonSerializer.Serialize(release, JSON),
                    ct
                )
                .ConfigureAwait(false);
        }

        await UpdateEnvironmentAsync(environment, x => x with { Version = version }, ct)
            .ConfigureAwait(false);
    }

    /// <summary>Removes releases past the newest kept, but none an environment is on.</summary>
    private async Task PruneAsync(CancellationToken ct)
    {
        var environments = await ReadEnvironmentsAsync(ct).ConfigureAwait(false);
        var used = environments
            .Values.Select(x => x.Version)
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);

        foreach (var release in ReadReleases().Skip(Math.Max(1, config.Value.ClientKeepReleases)))
        {
            if (
                used.Contains(release.File.Version)
                || _downloading.ContainsKey(release.File.Version)
            )
                continue;

            Directory.Delete(Path.Combine(ReleasesRoot, release.File.Version), recursive: true);
            logger.LogInformation(
                "Habbo client release {Version} removed: older than the ones kept",
                release.File.Version
            );
        }
    }

    private ReleaseFile? ReadRelease(string version)
    {
        var path = Path.Combine(ReleasesRoot, version, RELEASE_FILE);

        if (
            !IsVersion(version)
            || !File.Exists(path)
            || !File.Exists(Path.Combine(ReleasesRoot, version, ASAR_FILE))
        )
            return null;

        try
        {
            return JsonSerializer.Deserialize<ReleaseFile>(File.ReadAllText(path), JSON);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Every release downloaded, newest first.</summary>
    private List<(ReleaseFile File, HabboClientReleaseSnapshot Snapshot)> ReadReleases()
    {
        if (!Directory.Exists(ReleasesRoot))
            return [];

        return
        [
            .. Directory
                .EnumerateDirectories(ReleasesRoot)
                .Select(Path.GetFileName)
                .Where(x => x is not null && !x.EndsWith(".partial", StringComparison.Ordinal))
                .Select(x => ReadRelease(x!))
                .OfType<ReleaseFile>()
                .OrderByDescending(x => x.DownloadedAt)
                .Select(x =>
                    (
                        x,
                        new HabboClientReleaseSnapshot
                        {
                            Version = x.Version,
                            ReleaseId = x.ReleaseId,
                            Url = x.Url,
                            DownloadedAt = x.DownloadedAt,
                            BuiltAt = x.BuiltAt,
                            Environments = [.. x.Environments],
                            FileCount = x.Files.Count,
                            Libraries =
                            [
                                .. x
                                    .Files.Select(f => f.Path)
                                    .Where(p => p.EndsWith(".hab", StringComparison.Ordinal))
                                    .Order(StringComparer.Ordinal),
                            ],
                            Size = x.Size,
                        }
                    )
                ),
        ];
    }

    private async Task<Dictionary<string, EnvironmentState>> ReadEnvironmentsAsync(
        CancellationToken ct
    )
    {
        var path = Path.Combine(Root, ENVIRONMENTS_FILE);

        if (!File.Exists(path))
            return new(StringComparer.Ordinal);

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, EnvironmentState>>(
                    await File.ReadAllTextAsync(path, ct).ConfigureAwait(false),
                    JSON
                ) ?? new(StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            return new(StringComparer.Ordinal);
        }
    }

    private async Task UpdateEnvironmentAsync(
        string name,
        Func<EnvironmentState, EnvironmentState> change,
        CancellationToken ct
    )
    {
        await _state.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            var environments = await ReadEnvironmentsAsync(ct).ConfigureAwait(false);

            environments[name] = change(
                environments.GetValueOrDefault(name) ?? new EnvironmentState()
            );
            Directory.CreateDirectory(Root);

            var path = Path.Combine(Root, ENVIRONMENTS_FILE);
            var temp = $"{path}.{Guid.NewGuid():N}.part";

            await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(environments, JSON), ct)
                .ConfigureAwait(false);
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            _state.Release();
        }
    }

    private static bool IsVersion(string value) =>
        VersionPattern().IsMatch(value) && !value.StartsWith('.');

    [GeneratedRegex("^[A-Za-z0-9_.-]+$")]
    private static partial Regex VersionPattern();

    [GeneratedRegex("^[0-9a-f]{64}$")]
    private static partial Regex Sha256Pattern();

    private sealed record EnvironmentState
    {
        public DateTime? CheckedAt { get; init; }
        public string? LiveVersion { get; init; }
        public string? LiveUrl { get; init; }
        public string? AirVersion { get; init; }
        public string? AirUrl { get; init; }
        public string? Error { get; init; }
        public string? Version { get; init; }
    }

    private sealed record ReleaseFile
    {
        public required string Version { get; init; }
        public required string ReleaseId { get; init; }
        public required string Url { get; init; }
        public required DateTime DownloadedAt { get; init; }
        public DateTime? BuiltAt { get; init; }
        public required List<string> Environments { get; init; }
        public required List<ReleaseFileEntry> Files { get; init; }
        public required long Size { get; init; }
    }

    private sealed record ReleaseFileEntry(string Path, long Size, string Sha256);
}

/// <summary>
/// A downloaded release of Habbo's HTML client, opened: its version, the environment followed, the
/// archive, the client's folder in it, and each file's SHA-256 as its manifest lists it.
/// </summary>
internal sealed record HabboClientRelease(
    string Version,
    string Environment,
    AsarArchive Asar,
    string ClientRoot,
    ImmutableDictionary<string, string> FileHashes
)
{
    /// <summary>A file of the client (a path as its manifest lists it); null when it has none.</summary>
    public byte[]? Read(string path) => Asar.Read(ClientRoot + path);
}
