using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
    IOptions<AssetBundleConfig> config,
    TimeProvider time,
    ILogger<AssetPublishService> logger
) : IAssetPublishService
{
    /// <summary>The target a publish is running for, or zero: it can't be deleted under it.</summary>
    private int _publishing;

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
        var run = new AssetPublishRun(
            dbCtxFactory,
            store,
            connections,
            time,
            logger,
            target,
            password,
            config.Value.PublishConcurrency,
            player
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
    }

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
    }

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
