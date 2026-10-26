using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.Assets;
using Turbo.Assets.Nitro;
using Turbo.Database.Context;
using Turbo.Database.Entities.Assets;
using Turbo.Database.Extensions;
using Turbo.Gamedata.Configuration;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Gamedata.Enums;
using Turbo.Primitives.Gamedata.Snapshots;
using Turbo.Primitives.Players;

namespace Turbo.Gamedata.Assets.Publishing;

/// <summary>
/// <see cref="IAssetPublishService"/>: targets kept in <c>asset_publish_targets</c> with their
/// passwords sealed (<see cref="AssetPasswordSealer"/>), and publishes run as asset jobs
/// (<see cref="AssetPublishRun"/>).
/// </summary>
internal sealed class AssetPublishService(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IAssetBundleStore store,
    IAssetJobs jobs,
    AssetPasswordSealer sealer,
    PublishConnections connections,
    PublishedPets pets,
    PublishedFurnitureIcons icons,
    CloudflarePurge cloudflare,
    IOptions<AssetBundleConfig> config,
    TimeProvider time,
    ILogger<AssetPublishService> logger
) : IAssetPublishService
{
    /// <summary>Files a page of a target's pending list holds.</summary>
    public const int PENDING_PAGE_SIZE = 100;

    /// <summary>Changed files a "compare all" compares, largest first; it counts them all.</summary>
    public const int COMPARE_ALL_MAX = 100;

    /// <summary>The target a publish is running for, or zero: it can't be deleted under it.</summary>
    private int _publishing;

    private readonly TargetCopies _copies = new();

    public async Task<IReadOnlyList<AssetPublishTargetSnapshot>> ListTargetsAsync(
        CancellationToken ct
    )
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var targets = await dbCtx
            .AssetPublishTargets.AsNoTracking()
            .OrderBy(x => x.Name)
            .ThenBy(x => x.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return await SnapshotsAsync(dbCtx, targets, ct).ConfigureAwait(false);
    }

    public async Task<AssetPublishTargetSnapshot?> GetTargetAsync(
        int targetId,
        CancellationToken ct
    )
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var target = await dbCtx
            .AssetPublishTargets.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == targetId, ct)
            .ConfigureAwait(false);

        return target is null
            ? null
            : (await SnapshotsAsync(dbCtx, [target], ct).ConfigureAwait(false))[0];
    }

    public async Task<AssetPublishTargetSnapshot> CreateTargetAsync(
        AssetPublishTargetEdit edit,
        CancellationToken ct
    )
    {
        Validate(edit);

        var target = new AssetPublishTargetEntity { Name = "", Protocol = edit.Protocol };

        Apply(target, edit);

        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        dbCtx.AssetPublishTargets.Add(target);
        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

        logger.LogInformation(
            "Publish target {TargetId} ({Name}) added: {Where}",
            target.Id,
            target.Name,
            PublishConnections.Where(target)
        );

        return (await SnapshotsAsync(dbCtx, [target], ct).ConfigureAwait(false))[0];
    }

    public async Task<AssetPublishTargetSnapshot?> UpdateTargetAsync(
        int targetId,
        AssetPublishTargetEdit edit,
        CancellationToken ct
    )
    {
        Validate(edit);

        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var target = await dbCtx
            .AssetPublishTargets.SingleOrDefaultAsync(x => x.Id == targetId, ct)
            .ConfigureAwait(false);

        if (target is null)
            return null;

        Apply(target, edit);
        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

        logger.LogInformation(
            "Publish target {TargetId} ({Name}) changed: {Where}",
            target.Id,
            target.Name,
            PublishConnections.Where(target)
        );

        return (await SnapshotsAsync(dbCtx, [target], ct).ConfigureAwait(false))[0];
    }

    public async Task<bool> DeleteTargetAsync(int targetId, CancellationToken ct)
    {
        if (jobs.Running && Volatile.Read(ref _publishing) == targetId)
            throw new InvalidOperationException(
                "It is being published to; stop the publish first."
            );

        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var target = await dbCtx
            .AssetPublishTargets.SingleOrDefaultAsync(x => x.Id == targetId, ct)
            .ConfigureAwait(false);

        if (target is null)
            return false;

        // Its records and history go with it (the tables cascade).
        dbCtx.AssetPublishTargets.Remove(target);
        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

        logger.LogInformation("Publish target {TargetId} ({Name}) removed", target.Id, target.Name);

        return true;
    }

    public async Task<AssetPublishPending?> GetPendingAsync(
        int targetId,
        string? query,
        int page,
        CancellationToken ct
    )
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        if (
            !await dbCtx
                .AssetPublishTargets.AnyAsync(x => x.Id == targetId, ct)
                .ConfigureAwait(false)
        )
            return null;

        var files = await store.ListFilesAsync(ct).ConfigureAwait(false);
        var recorded = await dbCtx
            .AssetPublishedFiles.AsNoTracking()
            .Where(x => x.TargetEntityId == targetId)
            .ToDictionaryAsync(x => x.Path, x => x.Hash, StringComparer.Ordinal, ct)
            .ConfigureAwait(false);
        var pending = AssetPublishRun.Pending(files, recorded).ToList();
        var search = query?.Trim() ?? "";
        var matched = pending
            .Where(x => x.File.Path.Contains(search, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.File.Size)
            .ThenBy(x => x.File.Path, StringComparer.Ordinal)
            .ToList();

        return new AssetPublishPending
        {
            Items =
            [
                .. matched.Skip(Math.Max(0, page) * PENDING_PAGE_SIZE).Take(PENDING_PAGE_SIZE),
            ],
            Matched = matched.Count,
            PageSize = PENDING_PAGE_SIZE,
            Count = pending.Count,
            Bytes = pending.Sum(x => x.File.Size),
            New = pending.Count(x => x.Change == AssetPublishChange.New),
            Changed = pending.Count(x => x.Change == AssetPublishChange.Changed),
        };
    }

    public async Task<AssetPublishComparisons?> CompareAsync(
        int targetId,
        string? path,
        CancellationToken ct
    )
    {
        var target = await FindAsync(targetId, ct).ConfigureAwait(false);

        if (target is null)
            return null;

        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var files = await store.ListFilesAsync(ct).ConfigureAwait(false);
        var recorded = await dbCtx
            .AssetPublishedFiles.AsNoTracking()
            .Where(x => x.TargetEntityId == targetId)
            .ToDictionaryAsync(x => x.Path, x => x.Hash, StringComparer.Ordinal, ct)
            .ConfigureAwait(false);
        List<AssetBundleFile> compare;
        int total;

        if (path?.Trim() is { Length: > 0 } one)
        {
            if (files.FirstOrDefault(x => x.Path == one) is not { } file)
                return null;

            compare = [file];
            total = 1;
        }
        else
        {
            var changed = AssetPublishRun
                .Pending(files, recorded)
                .Where(x => x.Change == AssetPublishChange.Changed)
                .Select(x => x.File)
                .OrderByDescending(x => x.Size)
                .ThenBy(x => x.Path, StringComparer.Ordinal)
                .ToList();

            total = changed.Count;
            compare = [.. changed.Take(COMPARE_ALL_MAX)];
        }

        var names = compare.Select(x => x.Name).Distinct().ToList();
        var rows = (
            await dbCtx
                .AssetBundles.AsNoTracking()
                .Where(x => names.Contains(x.Name))
                .Select(x => new
                {
                    x.Kind,
                    x.Name,
                    x.Source,
                    x.Revision,
                    x.UpdatedAt,
                })
                .ToListAsync(ct)
                .ConfigureAwait(false)
        ).ToDictionary(x => (x.Kind, x.Name));
        var reader = ReaderOf(target);
        var items = new List<AssetPublishComparison>();

        await using (reader.ConfigureAwait(false))
        {
            foreach (var file in compare)
            {
                var hash = recorded.GetValueOrDefault(file.Path) ?? "";
                var (copy, problem) = await reader
                    .ReadAsync(file.Path, hash, ct)
                    .ConfigureAwait(false);
                var row = rows.GetValueOrDefault((file.Kind, file.Name));
                var compared = new AssetPublishComparison
                {
                    File = file,
                    Result = AssetCompareResult.Unreadable,
                    Problem = problem,
                    TargetBytes = copy?.LongLength,
                    TargetAsRecorded = copy is not null && GamedataBytes.Hash(copy) == hash,
                    Source = row?.Source,
                    Revision = row?.Revision,
                    UpdatedAt = row?.UpdatedAt,
                };

                items.Add(
                    copy is null
                        ? compared with
                        {
                            Result = problem is null
                                ? AssetCompareResult.NotOnTarget
                                : AssetCompareResult.Unreadable,
                        }
                        : await CompareCopyAsync(compared, copy, ct).ConfigureAwait(false)
                );
            }
        }

        return new AssetPublishComparisons([.. items], total);
    }

    /// <summary>The target's copy against the hotel's, read from the store.</summary>
    private async Task<AssetPublishComparison> CompareCopyAsync(
        AssetPublishComparison compared,
        byte[] copy,
        CancellationToken ct
    )
    {
        var file = compared.File;
        byte[] hotel;

        try
        {
            hotel = await File.ReadAllBytesAsync(store.FullPathOf(file.Kind, file.Name), ct)
                .ConfigureAwait(false);
        }
        catch (IOException ex)
        {
            return compared with { Problem = $"The hotel's copy could not be read: {ex.Message}" };
        }

        var found = BundleComparer.Compare(file.Name, copy, hotel, file.Kind.IsBundle());

        return compared with
        {
            Result = found.Result,
            Problem = found.Problem,
            StampBefore = found.StampBefore,
            StampAfter = found.StampAfter,
            Windows = found.Windows.IsDefault ? [] : found.Windows,
            Texts = found.Texts.IsDefault ? [] : found.Texts,
            Entries = found.Entries.IsDefault ? [] : found.Entries,
            EntriesChanged = found.EntriesChanged,
        };
    }

    public async Task<byte[]?> ReadComparedImageAsync(
        int targetId,
        string path,
        string? entry,
        bool fromTarget,
        CancellationToken ct
    )
    {
        if (!BundleComparer.IsImage(entry ?? path))
            return null;

        var target = await FindAsync(targetId, ct).ConfigureAwait(false);

        if (target is null)
            return null;

        var files = await store.ListFilesAsync(ct).ConfigureAwait(false);

        if (files.FirstOrDefault(x => x.Path == path) is not { } file)
            return null;

        byte[]? data;

        if (fromTarget)
        {
            var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
            await using var dbCtxScope = dbCtx.ConfigureAwait(false);

            var hash =
                await dbCtx
                    .AssetPublishedFiles.Where(x => x.TargetEntityId == targetId && x.Path == path)
                    .Select(x => x.Hash)
                    .FirstOrDefaultAsync(ct)
                    .ConfigureAwait(false)
                ?? "";
            var reader = ReaderOf(target);

            await using (reader.ConfigureAwait(false))
                (data, _) = await reader.ReadAsync(path, hash, ct).ConfigureAwait(false);
        }
        else
        {
            try
            {
                data = await File.ReadAllBytesAsync(store.FullPathOf(file.Kind, file.Name), ct)
                    .ConfigureAwait(false);
            }
            catch (IOException)
            {
                data = null;
            }
        }

        if (data is null || entry is null)
            return data;

        try
        {
            return NitroBundle.Read(data).Files.GetValueOrDefault(entry);
        }
        catch (AssetFormatException)
        {
            return null;
        }
    }

    private TargetReader ReaderOf(AssetPublishTargetEntity target) =>
        new(_copies, connections, dbCtxFactory, logger, target, PasswordOf(target));

    /// <summary>
    /// Reads copies from a target for comparing, kept in <see cref="_copies"/>: it connects on the
    /// first copy not kept, once, and a target that can't be reached is said for every copy after.
    /// </summary>
    private sealed class TargetReader(
        TargetCopies copies,
        PublishConnections connections,
        IDbContextFactory<TurboDbContext> dbCtxFactory,
        ILogger logger,
        AssetPublishTargetEntity target,
        string password
    ) : IAsyncDisposable
    {
        private IPublishConnection? _connection;
        private string? _unreachable;

        /// <summary>The copy, or null with why (no why: the target holds no such file).</summary>
        public async Task<(byte[]? Copy, string? Problem)> ReadAsync(
            string path,
            string hash,
            CancellationToken ct
        )
        {
            if (copies.TryGet(target.Id, path, hash, out var kept))
                return (kept, null);

            if (_connection is null && _unreachable is null)
            {
                try
                {
                    _connection = await connections
                        .ConnectAsync(target, password, ct)
                        .ConfigureAwait(false);
                    await AssetPublishRun
                        .TrustHostKeyAsync(dbCtxFactory, logger, target, _connection.HostKey, ct)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                    when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
                {
                    logger.LogWarning(
                        ex,
                        "Reading from publish target {TargetId} ({Where}) to compare failed",
                        target.Id,
                        PublishConnections.Where(target)
                    );
                    _unreachable = $"The target could not be reached: {ex.Message}";
                }
            }

            if (_connection is null)
                return (null, _unreachable);

            try
            {
                using var buffer = new TargetCopies.Buffer();

                await _connection.DownloadAsync(path, buffer, ct).ConfigureAwait(false);

                var copy = buffer.ToArray();

                copies.Put(target.Id, path, hash, copy);

                return (copy, null);
            }
            catch (FileNotFoundException)
            {
                return (null, null);
            }
            catch (Exception ex)
                when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                return (null, $"The target's copy could not be read: {ex.Message}");
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_connection is not null)
                await _connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    public async Task<AssetPublishTestResult?> TestAsync(int targetId, CancellationToken ct)
    {
        var target = await FindAsync(targetId, ct).ConfigureAwait(false);

        if (target is null)
            return null;

        try
        {
            var password = PasswordOf(target);
            var connection = await connections
                .ConnectAsync(target, password, ct)
                .ConfigureAwait(false);

            await using (connection.ConfigureAwait(false))
            {
                var names = await connection.ListAsync("", ct).ConfigureAwait(false);

                await AssetPublishRun
                    .TrustHostKeyAsync(dbCtxFactory, logger, target, connection.HostKey, ct)
                    .ConfigureAwait(false);

                return new AssetPublishTestResult(
                    true,
                    $"Connected to {PublishConnections.Where(target)}: {names.Count} entries there."
                );
            }
        }
        catch (Exception ex)
            when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(
                ex,
                "Testing publish target {TargetId} ({Where}) failed",
                target.Id,
                PublishConnections.Where(target)
            );

            return new AssetPublishTestResult(false, ex.Message);
        }
    }

    public async Task<AssetJobSnapshot?> PublishAsync(
        int targetId,
        bool dryRun,
        bool deleteRemoved,
        PlayerId player,
        CancellationToken ct
    )
    {
        var target = await FindAsync(targetId, ct).ConfigureAwait(false);

        if (target is null)
            return null;

        // Refused here rather than in the job, so a password to enter again is said at once.
        var password = PasswordOf(target);
        var purge = PurgeOf(target);
        var run = new AssetPublishRun(
            dbCtxFactory,
            store,
            connections,
            time,
            logger,
            target,
            password,
            config.Value.PublishConcurrency,
            player,
            purge
        );

        return jobs.Start(
            AssetJobKind.Publish,
            $"Publish to {target.Name}",
            player,
            async (progress, jobCt) =>
            {
                Volatile.Write(ref _publishing, target.Id);

                try
                {
                    // What it sends is made first: the furniture's icons, drawn from their bundles;
                    // and its pets are known to the client and the server before it sends them.
                    if (!dryRun)
                    {
                        progress.Log(await icons.BuildAsync(progress, jobCt).ConfigureAwait(false));
                        progress.Log(await pets.ApplyAsync(player, jobCt).ConfigureAwait(false));
                    }

                    return await run.RunAsync(dryRun, deleteRemoved, progress, jobCt)
                        .ConfigureAwait(false);
                }
                finally
                {
                    Interlocked.CompareExchange(ref _publishing, 0, target.Id);
                }
            }
        );
    }

    public async Task<bool> ForgetHostKeyAsync(int targetId, CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var changed = await dbCtx
            .AssetPublishTargets.Where(x => x.Id == targetId)
            .ExecuteUpdateAsync(x => x.SetProperty(t => t.HostKey, (string?)null), ct)
            .ConfigureAwait(false);

        if (changed > 0)
            logger.LogInformation("Publish target {TargetId} forgot its host key", targetId);

        return changed > 0;
    }

    public async Task<IReadOnlyList<AssetPublishSnapshot>?> GetHistoryAsync(
        int targetId,
        CancellationToken ct
    )
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        if (
            !await dbCtx
                .AssetPublishTargets.AnyAsync(x => x.Id == targetId, ct)
                .ConfigureAwait(false)
        )
            return null;

        var rows = await dbCtx
            .AssetPublishes.AsNoTracking()
            .Where(x => x.TargetEntityId == targetId)
            .OrderByDescending(x => x.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return [.. rows.Select(x => x.ToSnapshot())];
    }

    /// <summary>Refuses a target that could not be saved or reached as entered.</summary>
    private static void Validate(AssetPublishTargetEdit edit)
    {
        var name = edit.Name.Trim();

        if (name.Length == 0)
            throw new ArgumentException("Give the target a name.");

        if (name.Length > AssetPublishTargetEntity.NAME_MAX_LENGTH)
            throw new ArgumentException(
                $"A name can be at most {AssetPublishTargetEntity.NAME_MAX_LENGTH} characters."
            );

        if (!Enum.IsDefined(edit.Protocol))
            throw new ArgumentException("That protocol isn't one this server knows.");

        if (edit.Protocol == AssetPublishProtocol.Folder)
        {
            if (!Path.IsPathFullyQualified(edit.RemotePath.Trim()))
                throw new ArgumentException(
                    "A folder target needs the folder's full path on this server."
                );
        }
        else if (edit.Host.Trim().Length == 0)
            throw new ArgumentException("Give the server's host name.");

        if (edit.Port is < 0 or > ushort.MaxValue)
            throw new ArgumentException($"A port is between 0 and {ushort.MaxValue}.");

        if (edit.Host.Trim().Length > AssetPublishTargetEntity.HOST_MAX_LENGTH)
            throw new ArgumentException(
                $"A host can be at most {AssetPublishTargetEntity.HOST_MAX_LENGTH} characters."
            );

        if (edit.User.Trim().Length > AssetPublishTargetEntity.USER_MAX_LENGTH)
            throw new ArgumentException(
                $"A user can be at most {AssetPublishTargetEntity.USER_MAX_LENGTH} characters."
            );

        if (
            edit.RemotePath.Trim().Length > AssetPublishTargetEntity.PATH_MAX_LENGTH
            || edit.PublicUrl.Trim().Length > AssetPublishTargetEntity.PATH_MAX_LENGTH
        )
            throw new ArgumentException(
                $"A path or address can be at most {AssetPublishTargetEntity.PATH_MAX_LENGTH} characters."
            );

        var zone = edit.CloudflareZoneId.Trim();

        if (zone.Length > 0)
        {
            if (!IsZoneId(zone))
                throw new ArgumentException(
                    "A Cloudflare zone id is the 32 letters and digits (0-9, a-f) its dashboard shows."
                );

            if (edit.PublicUrl.Trim().Length == 0)
                throw new ArgumentException(
                    "Purging Cloudflare needs the public URL, to know the addresses to purge."
                );
        }
    }

    private static bool IsZoneId(string zone) =>
        zone.Length == 32 && zone.All(char.IsAsciiHexDigit);

    private void Apply(AssetPublishTargetEntity target, AssetPublishTargetEdit edit)
    {
        var host = edit.Protocol == AssetPublishProtocol.Folder ? "" : edit.Host.Trim();

        // Pointed somewhere else, it trusts the key it is shown there afresh.
        if (
            target.Protocol != edit.Protocol
            || !string.Equals(target.Host, host, StringComparison.OrdinalIgnoreCase)
            || target.Port != edit.Port
        )
            target.HostKey = null;

        target.Name = edit.Name.Trim();
        target.Protocol = edit.Protocol;
        target.Host = host;
        target.Port = edit.Port;
        target.User = edit.User.Trim();
        target.RemotePath = edit.RemotePath.Trim();
        target.PublicUrl = edit.PublicUrl.Trim();
        target.AllowSelfSigned = edit.Protocol == AssetPublishProtocol.Ftps && edit.AllowSelfSigned;

        if (edit.Password is { } password)
            target.Password = password.Length == 0 ? null : sealer.Seal(password);

        var zone = edit.CloudflareZoneId.Trim().ToLowerInvariant();

        target.CloudflareZoneId = zone.Length == 0 ? null : zone;

        if (edit.CloudflareToken is { } token)
            target.CloudflareToken = token.Trim().Length == 0 ? null : sealer.Seal(token.Trim());
    }

    /// <summary>
    /// What the target's publishes ask Cloudflare to purge, when it has a zone and a token; null
    /// when it purges nothing.
    /// </summary>
    private AssetPublishPurge? PurgeOf(AssetPublishTargetEntity target) =>
        target.CloudflareZoneId is { Length: > 0 } zone
        && target.CloudflareToken is { Length: > 0 } sealedToken
        && target.PublicUrl.Length > 0
            ? new AssetPublishPurge(cloudflare, zone, sealer.Unseal(sealedToken))
            : null;

    /// <summary>The target's password, unsealed; empty when it has none.</summary>
    private string PasswordOf(AssetPublishTargetEntity target) =>
        target.Password is { Length: > 0 } sealedBytes ? sealer.Unseal(sealedBytes) : "";

    private async Task<AssetPublishTargetEntity?> FindAsync(int targetId, CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        return await dbCtx
            .AssetPublishTargets.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == targetId, ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// The targets with how many bundles each lacks or holds an older copy of, and its newest
    /// publish: the store listed once, and the records and newest publishes of all of them read
    /// together.
    /// </summary>
    private async Task<IReadOnlyList<AssetPublishTargetSnapshot>> SnapshotsAsync(
        TurboDbContext dbCtx,
        IReadOnlyList<AssetPublishTargetEntity> targets,
        CancellationToken ct
    )
    {
        if (targets.Count == 0)
            return [];

        var ids = targets.Select(x => x.Id).ToList();
        var files = await store.ListFilesAsync(ct).ConfigureAwait(false);
        var records = await dbCtx
            .AssetPublishedFiles.AsNoTracking()
            .Where(x => ids.Contains(x.TargetEntityId))
            .Select(x => new
            {
                x.TargetEntityId,
                x.Path,
                x.Hash,
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var recordsByTarget = records
            .GroupBy(x => x.TargetEntityId)
            .ToDictionary(
                g => g.Key,
                g =>
                    (IReadOnlyDictionary<string, string>)
                        g.ToDictionary(x => x.Path, x => x.Hash, StringComparer.Ordinal)
            );
        var newestIds = await dbCtx
            .AssetPublishes.Where(x => ids.Contains(x.TargetEntityId))
            .GroupBy(x => x.TargetEntityId)
            .Select(g => g.Max(x => x.Id))
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var newest = await dbCtx
            .AssetPublishes.AsNoTracking()
            .Where(x => newestIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.TargetEntityId, ct)
            .ConfigureAwait(false);
        var none = new Dictionary<string, string>();

        return
        [
            .. targets.Select(x =>
                x.ToSnapshot(
                    AssetPublishRun
                        .ToSend(files, recordsByTarget.GetValueOrDefault(x.Id) ?? none)
                        .Count(),
                    newest.GetValueOrDefault(x.Id)?.ToSnapshot()
                )
            ),
        ];
    }
}
