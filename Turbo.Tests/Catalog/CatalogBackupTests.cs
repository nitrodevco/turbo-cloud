using FluentAssertions;
using Orleans;
using Turbo.Catalog.Editing;
using Turbo.Database.Entities.Catalog;
using Turbo.Primitives.Catalog.Editing;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Tests.Support;
using Xunit;
using static Turbo.Tests.Catalog.CatalogFixture;

namespace Turbo.Tests.Catalog;

/// <summary>
/// Backing the catalog up and rolling back to it: a backup outlives the editor that took it (a
/// publish, a restart), and rolling back puts every page, offer, product and featured item back
/// as it was, ids included, as one step that can be undone; what it replaced is backed up first.
/// A rollback is refused, leaving the catalog as it is, when it would take away an offer that
/// sells a limited series or name furniture the hotel no longer has.
/// </summary>
public sealed class CatalogBackupTests : IDisposable
{
    private static readonly PlayerId Editor = 1;

    private readonly CatalogFixture _catalog = new();
    private readonly CatalogEditService _service;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public CatalogBackupTests()
    {
        _catalog.Fakes.Handlers["GetOnlinePlayerIds"] = _ => (IReadOnlyCollection<PlayerId>)[];
        _service = NewService();
    }

    public void Dispose() => _catalog.Dispose();

    /// <summary>The editor as a server start makes it, with no history.</summary>
    private CatalogEditService NewService() =>
        new(
            _catalog.Db,
            _catalog.Definitions,
            _catalog.NormalProvider(),
            _catalog.BuildersClubProvider(),
            _catalog.Fakes.Create<ISessionGateway>(),
            _catalog.Fakes.Create<IGrainFactory>(),
            new CapturingLogger<ICatalogEditService>()
        );

    private static CatalogOfferDraft PosterOffer(int pageId) =>
        new(
            pageId,
            string.Empty,
            3,
            0,
            null,
            true,
            true,
            0,
            true,
            new CatalogProductDraft(ProductType.Wall, POSTER, null, 1)
        );

    /// <summary>Edits of every kind, published, so the history can't take them back.</summary>
    private async Task<int> EditAndPublishAsync()
    {
        (await _service.MovePageAsync(Editor, CHILD, HIDDEN_PAGE, 0, Ct)).Saved.Should().BeTrue();
        (await _service.DeleteOfferAsync(Editor, SOLD, Ct)).Saved.Should().BeTrue();
        (
            await _service.SaveFeaturedItemsAsync(
                Editor,
                [new("New", "promo.png", CatalogFrontPageItemType.Page, "chairs", null)],
                Ct
            )
        )
            .Saved.Should()
            .BeTrue();

        var created = await _service.CreateOfferAsync(Editor, PosterOffer(FURNITURE), Ct);

        created.Saved.Should().BeTrue();
        await _service.PublishAsync(Editor, Ct);

        return created.Id;
    }

    [Fact]
    public async Task RollingBack_AfterAPublishAndARestart_PutsBackEveryRowAsTheBackupHasIt()
    {
        var before = _catalog.Rows();
        var backup = await _service.BackupAsync(Editor, "Before the sale", Ct);

        backup.Saved.Should().BeTrue();
        await EditAndPublishAsync();
        _catalog.Rows().Should().NotBe(before);

        var restarted = NewService();
        var rolledBack = await restarted.RollbackAsync(Editor, backup.Id, Ct);

        rolledBack.Error.Should().BeNull();
        _catalog.Rows().Should().Be(before);
        restarted.UnpublishedChanges.Should().Be(1, "a rollback goes live when published");
        restarted
            .History.Undo.Select(x => x.Label)
            .Should()
            .Equal("rolled back to the backup Before the sale");
    }

    [Fact]
    public async Task ARollback_CanBeUndone_AndWhatItReplacedIsBackedUpFirst()
    {
        var backup = await _service.BackupAsync(Editor, "Before the sale", Ct);
        var newOffer = await EditAndPublishAsync();
        var edited = _catalog.Rows();

        (await _service.RollbackAsync(Editor, backup.Id, Ct)).Saved.Should().BeTrue();

        var backups = await _service.GetBackupsAsync(Ct);

        backups
            .Select(x => (x.Name, x.Automatic))
            .Should()
            .Equal(("Before rolling back to Before the sale", true), ("Before the sale", false));
        backups[0].Offers.Should().Be(6, "five of the fixture's offers stayed and one was made");
        backups[0].TakenBy.Should().Be(Editor);

        (await _service.UndoAsync(Editor, Ct)).Error.Should().BeNull();
        _catalog.Rows().Should().Be(edited);

        // And what was replaced can be rolled back to, after a publish too.
        (await _service.RedoAsync(Editor, Ct))
            .Saved.Should()
            .BeTrue();
        await _service.PublishAsync(Editor, Ct);
        (await _service.RollbackAsync(Editor, backups[0].Id, Ct)).Error.Should().BeNull();
        _catalog.Rows().Should().Be(edited);
        _catalog.Rows().Should().Contain($"offer {newOffer} {FURNITURE}");
    }

    [Fact]
    public async Task ABackup_WithNoName_IsNamedAfterWhenItWasTaken()
    {
        var backup = await _service.BackupAsync(Editor, "  ", Ct);

        backup.Saved.Should().BeTrue();
        (await _service.GetBackupsAsync(Ct)).Single().Name.Should().StartWith("Backup of ");
    }

    [Fact]
    public async Task RollingBack_ToTheCatalogAsItIs_IsRefused()
    {
        var backup = await _service.BackupAsync(Editor, "Now", Ct);

        var rolledBack = await _service.RollbackAsync(Editor, backup.Id, Ct);

        rolledBack.Saved.Should().BeFalse();
        rolledBack.Error.Should().Contain("already");
        (await _service.GetBackupsAsync(Ct)).Should().HaveCount(1, "nothing was replaced");
    }

    [Fact]
    public async Task RollingBack_IsRefused_WhenItWouldTakeAwayAnOfferThatSellsALimitedSeries()
    {
        var backup = await _service.BackupAsync(Editor, "Before", Ct);
        var created = await _service.CreateOfferAsync(Editor, PosterOffer(CHILD), Ct);

        using (var db = _catalog.Db.CreateDbContext())
        {
            var product = db.CatalogProducts.Single(x => x.CatalogOfferEntityId == created.Id);

            db.LtdSeries.Add(
                new LtdSeriesEntity
                {
                    CatalogProductEntityId = product.Id,
                    TotalQuantity = 10,
                    RemainingQuantity = 10,
                    RaffleWindowSeconds = 30,
                    IsActive = true,
                }
            );
            db.SaveChanges();
        }

        var after = _catalog.Rows();
        var rolledBack = await _service.RollbackAsync(Editor, backup.Id, Ct);

        rolledBack.Saved.Should().BeFalse();
        rolledBack.Error.Should().Contain("limited series");
        _catalog.Rows().Should().Be(after);
        (await _service.GetBackupsAsync(Ct)).Should().HaveCount(1);
    }

    [Fact]
    public async Task RollingBack_IsRefused_WhenTheBackupSellsFurnitureThatIsGone()
    {
        var gone = _catalog.AddDefinition(50, "gone_chair");
        var created = await _service.CreateOfferAsync(
            Editor,
            PosterOffer(CHILD) with
            {
                Product = new CatalogProductDraft(ProductType.Floor, gone.Id, null, 1),
            },
            Ct
        );

        created.Error.Should().BeNull();

        var backup = await _service.BackupAsync(Editor, "With the chair", Ct);

        (await _service.DeleteOfferAsync(Editor, created.Id, Ct)).Saved.Should().BeTrue();

        using (var db = _catalog.Db.CreateDbContext())
        {
            db.FurnitureDefinitions.Remove(db.FurnitureDefinitions.Single(x => x.Id == gone.Id));
            db.SaveChanges();
        }

        var rolledBack = await _service.RollbackAsync(Editor, backup.Id, Ct);

        rolledBack.Saved.Should().BeFalse();
        rolledBack.Error.Should().Contain("furniture the hotel no longer has").And.Contain("50");
        _catalog.Rows().Should().NotContain($"offer {created.Id} ");
    }

    [Fact]
    public async Task ADeletedBackup_IsGone()
    {
        var backup = await _service.BackupAsync(Editor, "Old", Ct);

        (await _service.DeleteBackupAsync(Editor, backup.Id, Ct)).Saved.Should().BeTrue();

        (await _service.GetBackupsAsync(Ct)).Should().BeEmpty();
        (await _service.RollbackAsync(Editor, backup.Id, Ct))
            .Error.Should()
            .Be("That backup is gone.");
    }
}
