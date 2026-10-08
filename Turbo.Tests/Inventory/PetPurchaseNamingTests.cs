using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Turbo.Database.Achievements;
using Turbo.Database.Entities.Players;
using Turbo.Inventory;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Inventory.Factories;
using Turbo.Primitives.Inventory.Grains;
using Turbo.Primitives.Messages.Outgoing.Inventory.Pets;
using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Pets.Enums;
using Turbo.Primitives.Pets.Providers;
using Turbo.Primitives.Pets.Snapshots;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Inventory;

/// <summary>
/// A catalog pet page asks for its name to be approved before the purchase is confirmed, and is
/// answered by the rules the purchase itself is held to, with the broken limit for the client's
/// message. A pet that is bought tells the buyer so.
/// </summary>
public sealed class PetPurchaseNamingTests : IDisposable
{
    private const int BUYER = 7;
    private const int OFFER_ID = 900;
    private const int PET_TYPE = 0;
    private const int PALETTE = 3;

    private static readonly BindingFlags All =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public PetPurchaseNamingTests()
    {
        _db.Insert(
            new PlayerEntity
            {
                Id = BUYER,
                Name = "buyer",
                Figure = "hd-180-1",
                Gender = AvatarGenderType.Male,
                PlayerStatus = PlayerStatusType.Offline,
            }
        );

        _fakes.Handlers["GetPlayerNameAsync"] = _ => Task.FromResult<string?>("buyer");
        _fakes.Handlers["TryGetPalette"] = call =>
            (int)call.Args[0]! == PET_TYPE && (int)call.Args[1]! == PALETTE
                ? new PetBreedSnapshot
                {
                    TypeId = PET_TYPE,
                    BreedId = PALETTE,
                    PaletteId = PALETTE,
                    RarityLevel = 0,
                    Sellable = true,
                    Rare = false,
                    ColorTag = -1,
                }
                : null;
    }

    public void Dispose() => _db.Dispose();

    [Theory]
    [InlineData("", PetNameValidationType.TooShort, "1")]
    [InlineData("   ", PetNameValidationType.TooShort, "1")]
    [InlineData("Sixteen letters!", PetNameValidationType.TooLong, "15")]
    [InlineData("Rex<3", PetNameValidationType.InvalidCharacters, "")]
    public async Task ARefusedName_IsAnsweredWithTheReason_AndTheLimitItBroke(
        string name,
        PetNameValidationType result,
        string info
    )
    {
        await NewInventory().SendPetNameApprovalAsync(name, Ct);

        Sent<ApproveNameMessageComposer>()
            .Should()
            .ContainSingle()
            .Which.Should()
            .Be(new ApproveNameMessageComposer { Result = result, NameValidationInfo = info });
    }

    [Fact]
    public async Task ANameHoldingAFilteredWord_IsForbidden()
    {
        var inventory = NewInventory();

        RoomHarness.SetField(inventory, "_wordFilter", new NothingIsCleanFilter());

        await inventory.SendPetNameApprovalAsync("Rex", Ct);

        Sent<ApproveNameMessageComposer>()
            .Should()
            .ContainSingle()
            .Which.Result.Should()
            .Be(PetNameValidationType.Forbidden);
    }

    [Fact]
    public async Task AnAcceptableName_IsApproved()
    {
        await NewInventory().SendPetNameApprovalAsync("Rex Junior", Ct);

        Sent<ApproveNameMessageComposer>()
            .Should()
            .ContainSingle()
            .Which.Should()
            .Be(
                new ApproveNameMessageComposer
                {
                    Result = PetNameValidationType.Ok,
                    NameValidationInfo = string.Empty,
                }
            );
    }

    [Fact]
    public async Task ABoughtPet_TellsTheBuyerTheyBoughtIt()
    {
        await NewInventory().GrantCatalogOfferAsync(PetOffer(), $"Rex\n{PALETTE}\nFFFFFF", 1, Ct);

        var received = Sent<PetReceivedMessageComposer>().Should().ContainSingle().Subject;

        received.BoughtAsGift.Should().BeFalse();
        received.Pet.Name.Should().Be("Rex");
        received.Pet.Figure.PaletteId.Should().Be(PALETTE);
    }

    [Fact]
    public async Task ARefusedPurchase_TellsTheBuyerNothing()
    {
        var purchase = () =>
            NewInventory().GrantCatalogOfferAsync(PetOffer(), $"Rex<3\n{PALETTE}\nFFFFFF", 1, Ct);

        await purchase.Should().ThrowAsync<Exception>();
        Sent<PetReceivedMessageComposer>().Should().BeEmpty();
    }

    private IEnumerable<T> Sent<T>() =>
        _fakes
            .Log.Of("SendComposerAsync")
            .Where(x => (long)x.Key! == BUYER)
            .SelectMany(x => x.Args[0] is IEnumerable<object> many ? many : [x.Args[0]!])
            .OfType<T>();

    private static CatalogOfferSnapshot PetOffer()
    {
        var product = new CatalogProductSnapshot
        {
            Id = OFFER_ID,
            OfferId = OFFER_ID,
            ProductType = ProductType.Pet,
            FurniDefinitionId = 0,
            SpriteId = 0,
            ExtraParam = PET_TYPE.ToString(),
            Quantity = 1,
            UniqueSize = 0,
            UniqueRemaining = 0,
            ClassName = null,
        };

        return new CatalogOfferSnapshot
        {
            Id = OFFER_ID,
            PageId = 1,
            LocalizationId = "offer",
            Rentable = false,
            CostCredits = 1,
            CostSilver = 0,
            CostCurrency = 0,
            ActivityPointType = null,
            CanGift = false,
            CanBundle = false,
            ClubLevel = 0,
            Visible = true,
            ProductIds = [product.Id],
            Products = [product],
        };
    }

    /// <summary>The buyer's inventory grain with its real pet section over this database.</summary>
    private IInventoryGrain NewInventory()
    {
        var grain = GrainHarness.Create(
            typeof(InventoryModule).Assembly,
            "Turbo.Inventory.Grains.InventoryGrain",
            _fakes,
            _db,
            BUYER
        );

        RoomHarness.SetField(grain, "_achievementFacts", _fakes.Create<IAchievementFactRecorder>());

        var module = Activator.CreateInstance(
            typeof(InventoryModule).Assembly.GetType(
                "Turbo.Inventory.Grains.Modules.InventoryPetModule"
            )!,
            All,
            null,
            [
                grain,
                RoomHarness.GetMember(grain, "_state")!,
                _db,
                _fakes.Create<IPetBreedProvider>(),
                NullLogger.Instance,
            ],
            null
        )!;

        RoomHarness.SetField(grain, "PetModule", module);

        // The purchase hands an empty furniture list to the furni section after the pets.
        var furniModule = Activator.CreateInstance(
            typeof(InventoryModule).Assembly.GetType(
                "Turbo.Inventory.Grains.Modules.InventoryFurniModule"
            )!,
            All,
            null,
            [
                grain,
                RoomHarness.GetMember(grain, "_state")!,
                _db,
                _fakes.Create<IInventoryFurnitureLoader>(),
                _fakes.Create<IFurnitureDefinitionProvider>(),
                _fakes.Create<ICatalogService>(),
                NullLogger.Instance,
            ],
            null
        )!;

        RoomHarness.SetField(grain, "FurniModule", furniModule);

        return (IInventoryGrain)grain;
    }

    /// <summary>A hotel filter that finds a forbidden word in everything.</summary>
    private sealed class NothingIsCleanFilter : IWordFilter
    {
        public string Replacement => "bobba";

        public string Filter(string text) => Replacement;

        public string Filter(string text, IReadOnlySet<string> extraWords) => Replacement;

        public bool IsClean(string text) => false;

        public Task ReloadAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
