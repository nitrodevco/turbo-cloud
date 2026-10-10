using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Database.Entities.Assets;
using Turbo.Gamedata.Assets;
using Turbo.Gamedata.Assets.Publishing;
using Turbo.Gamedata.Configuration;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Gamedata.Enums;
using Turbo.Primitives.Gamedata.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Tests.Support;

namespace Turbo.Tests.Assets;

/// <summary>
/// A hotel's bundle store and publishing as the server builds them, in a temp folder over a SQLite
/// database: the real store, jobs, sealer, connections and publish service. Bundles are written to
/// the store with their rows; <see cref="TargetFolder"/> is an empty folder to publish to.
/// </summary>
internal sealed class AssetPublishHotel : IDisposable
{
    public static readonly PlayerId STAFF = new(7);

    private readonly string _temp = Directory
        .CreateTempSubdirectory("turbo-asset-publish-")
        .FullName;

    public AssetPublishHotel(int concurrency = 2)
    {
        Directory.CreateDirectory(TargetFolder);

        var config = Options.Create(
            new AssetBundleConfig
            {
                Directory = Path.Combine(_temp, "store"),
                PublishConcurrency = concurrency,
                PublishTimeoutSeconds = 5,
            }
        );

        Store = new AssetBundleStore(Db, config, new TestEnvironment(_temp));
        Jobs = new AssetJobs(
            config,
            new Fakes().Create<IHostApplicationLifetime>(),
            TimeProvider.System,
            NullLogger<IAssetJobs>.Instance
        );
        Publishing = new AssetPublishService(
            Db,
            Store,
            Jobs,
            new AssetPasswordSealer(config, Store, NullLogger<AssetPasswordSealer>.Instance),
            new PublishConnections(config, NullLogger<PublishConnections>.Instance),
            config,
            TimeProvider.System,
            NullLogger<AssetPublishService>.Instance
        );
    }

    public SqliteDb Db { get; } = new();

    public AssetBundleStore Store { get; }

    public AssetJobs Jobs { get; }

    public AssetPublishService Publishing { get; }

    public string TargetFolder => Path.Combine(_temp, "web");

    public string TargetFileOf(string name) =>
        Path.Combine(TargetFolder, "bundled", "furniture", $"{name}.nitro");

    /// <summary>Writes a furniture bundle to the store and saves its row, or replaces both.</summary>
    public async Task PutBundleAsync(string name, byte[] bundle, CancellationToken ct)
    {
        var hash = await Store.WriteAsync(AssetBundleKind.Furniture, name, bundle, ct);

        await using var dbCtx = await Db.CreateDbContextAsync(ct);

        var row = await dbCtx.AssetBundles.SingleOrDefaultAsync(
            x => x.Kind == AssetBundleKind.Furniture && x.Name == name,
            ct
        );

        if (row is null)
            dbCtx.AssetBundles.Add(
                row = new AssetBundleEntity
                {
                    Kind = AssetBundleKind.Furniture,
                    Name = name,
                    Source = AssetBundleSource.Habbo,
                }
            );

        row.Hash = hash;
        row.Size = bundle.Length;
        row.UpdatedAt = DateTime.UtcNow;

        await dbCtx.SaveChangesAsync(ct);
    }

    /// <summary>Removes a bundle from the store, its file and its row, as deleting it in the panel does.</summary>
    public async Task DropBundleAsync(string name, CancellationToken ct)
    {
        Store.Delete(AssetBundleKind.Furniture, name);

        await using var dbCtx = await Db.CreateDbContextAsync(ct);

        await dbCtx
            .AssetBundles.Where(x => x.Kind == AssetBundleKind.Furniture && x.Name == name)
            .ExecuteDeleteAsync(ct);
    }

    public Task<AssetPublishTargetSnapshot> AddFolderTargetAsync(
        CancellationToken ct,
        string? folder = null
    ) =>
        Publishing.CreateTargetAsync(
            new AssetPublishTargetEdit
            {
                Name = "Web root",
                Protocol = AssetPublishProtocol.Folder,
                RemotePath = folder ?? TargetFolder,
            },
            ct
        );

    /// <summary>Publishes to the target and waits for the job to end.</summary>
    public async Task<AssetJobSnapshot> PublishAsync(
        int targetId,
        CancellationToken ct,
        bool dryRun = false,
        bool deleteRemoved = false
    )
    {
        var started = await Publishing.PublishAsync(targetId, dryRun, deleteRemoved, STAFF, ct);

        started.Should().NotBeNull();

        return await EndOfJobAsync(ct);
    }

    public async Task<AssetJobSnapshot> EndOfJobAsync(CancellationToken ct)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);

        limit.CancelAfter(TimeSpan.FromSeconds(30));

        while (Jobs.Running)
            await Task.Delay(TimeSpan.FromMilliseconds(20), limit.Token);

        return Jobs.Current!;
    }

    public async Task<Dictionary<string, string>> RecordedAsync(int targetId, CancellationToken ct)
    {
        await using var dbCtx = await Db.CreateDbContextAsync(ct);

        return await dbCtx
            .AssetPublishedFiles.Where(x => x.TargetEntityId == targetId)
            .ToDictionaryAsync(x => x.Path, x => x.Hash, ct);
    }

    public void Dispose()
    {
        Db.Dispose();

        try
        {
            Directory.Delete(_temp, recursive: true);
        }
        catch (IOException)
        {
            // A file still held open by the OS for a moment; the temp folder is the OS's to clear.
        }
    }

    private sealed class TestEnvironment(string root) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Test";
        public string ApplicationName { get; set; } = "Turbo.Tests";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
