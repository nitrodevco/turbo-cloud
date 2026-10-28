using System.Net.Http;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Database.Entities.Gamedata;
using Turbo.Gamedata;
using Turbo.Gamedata.Configuration;
using Turbo.Gamedata.Furniture;
using Turbo.Gamedata.Habbo;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Gamedata.Enums;
using Turbo.Primitives.Gamedata.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Gamedata;

/// <summary>
/// Habbo's texts, product data and figure data are each taken in as a job of one step, in the
/// background: the panel asks for the job while it runs, and no other import starts meanwhile.
/// </summary>
public sealed class GamedataImportJobsTests : IDisposable
{
    private static readonly PlayerId STAFF = new(7);
    private static readonly byte[] CONTENT = [1, 2, 3];

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();
    private readonly TaskCompletionSource<GamedataChangeSetSnapshot?> _import = new(
        TaskCreationOptions.RunContinuationsAsynchronously
    );
    private readonly FurnitureImportJobs _jobs;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public GamedataImportJobsTests()
    {
        var config = Options.Create(new GamedataConfig());

#pragma warning disable VSTHRD003 // The import is held open until the test completes it.
        _fakes.Handlers["ImportAsync"] = _ => _import.Task;
#pragma warning restore VSTHRD003
        _jobs = new FurnitureImportJobs(
            _db,
            _fakes.Create<IGamedataFurnitureService>(),
            _fakes.Create<IGamedataTextService>(),
            _fakes.Create<IGamedataProductService>(),
            _fakes.Create<IGamedataFigureService>(),
            new HabboFurnitureFiles(
                _db,
                config,
                new HabboGamedataClient(
                    _fakes.Create<IHttpClientFactory>(),
                    NullLogger<HabboGamedataClient>.Instance
                ),
                NullLogger<HabboFurnitureFiles>.Instance
            ),
            _fakes.Create<IHostApplicationLifetime>(),
            TimeProvider.System,
            NullLogger<FurnitureImportJobs>.Instance
        );
    }

    public void Dispose() => _db.Dispose();

    [Theory]
    [InlineData(GamedataFiles.EXTERNAL_TEXTS, "texts")]
    [InlineData(GamedataFiles.PRODUCT_DATA, "product data")]
    [InlineData(GamedataFiles.FIGURE_DATA, "figure data")]
    public async Task a_version_is_taken_in_by_its_service_as_one_job_at_a_time(
        string file,
        string what
    )
    {
        var hash = GamedataBytes.Hash(CONTENT);

        Insert(file, 4, hash);

        (await StartAsync(file, 5)).Should().BeNull("there is no such version");

        var job = await StartAsync(file, 4);

        job.Should()
            .BeEquivalentTo(
                new
                {
                    ReleaseId = 4,
                    Revision = $"{what} {hash[..10]}",
                    File = file,
                    Phase = GamedataImportPhase.Import,
                    FilesTotal = 0,
                }
            );
        _jobs.Current.Should().Be(job);

        var again = () => StartAsync(file, 4);

        await again
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("Something of Habbo's is being taken in; wait for it to finish.");

        var changeSet = new GamedataChangeSetSnapshot
        {
            Id = 9,
            Kind = GamedataChangeKind.Import,
            Summary = "taken in",
            ChangeCount = 3,
            CreatedAt = DateTime.UtcNow,
        };

        _import.SetResult(changeSet);

        var done = await DoneAsync();

        done.Phase.Should().Be(GamedataImportPhase.Done);
        done.ChangeSet.Should().Be(changeSet);
        _fakes
            .Log.Of("ImportAsync")
            .Should()
            .ContainSingle()
            .Which.Args.Should()
            .Equal(4, (PlayerId?)STAFF, CancellationToken.None);
    }

    [Fact]
    public async Task a_job_whose_import_throws_fails_with_its_message_and_lets_the_next_start()
    {
        Insert(GamedataFiles.EXTERNAL_TEXTS, 4, GamedataBytes.Hash(CONTENT));

        await StartAsync(GamedataFiles.EXTERNAL_TEXTS, 4);

        _import.SetException(new InvalidOperationException("Habbo's file doesn't read."));

        var failed = await DoneAsync();

        failed.Phase.Should().Be(GamedataImportPhase.Failed);
        failed.Error.Should().Be("Habbo's file doesn't read.");
        (await StartAsync(GamedataFiles.EXTERNAL_TEXTS, 4)).Should().NotBeNull();
    }

    private Task<GamedataImportJobSnapshot?> StartAsync(string file, int versionId) =>
        file switch
        {
            GamedataFiles.EXTERNAL_TEXTS => _jobs.StartTextsAsync(versionId, STAFF, Ct),
            GamedataFiles.PRODUCT_DATA => _jobs.StartProductsAsync(versionId, STAFF, Ct),
            _ => _jobs.StartFiguresAsync(versionId, STAFF, Ct),
        };

    private async Task<GamedataImportJobSnapshot> DoneAsync()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);

        timeout.CancelAfter(TimeSpan.FromSeconds(10));

        while (_jobs.Current is { Phase: GamedataImportPhase.Import })
            await Task.Delay(10, timeout.Token);

        return _jobs.Current!;
    }

    private void Insert(string file, int id, string hash) =>
        _db.Insert(
            file switch
            {
                GamedataFiles.EXTERNAL_TEXTS => new HabboTextVersionEntity
                {
                    Id = id,
                    Domain = "com",
                    Hash = hash,
                    Content = CONTENT,
                    CheckedAt = DateTime.UtcNow,
                },
                GamedataFiles.PRODUCT_DATA => new HabboProductVersionEntity
                {
                    Id = id,
                    Domain = "com",
                    Hash = hash,
                    Content = CONTENT,
                    CheckedAt = DateTime.UtcNow,
                },
                _ => (object)
                    new HabboFigureVersionEntity
                    {
                        Id = id,
                        Domain = "com",
                        Hash = hash,
                        Content = CONTENT,
                        CheckedAt = DateTime.UtcNow,
                    },
            }
        );
}
