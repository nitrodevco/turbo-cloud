using System.IO;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.PacketHandlers.Camera;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Furniture.Snapshots.StuffData;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Messages.Incoming.Camera;
using Turbo.Primitives.Messages.Outgoing.Camera;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.PacketHandlers;

/// <summary>
/// A bought photo is a poster whose state the photo viewer reads (<c>ExternalImageWidget.loadPhoto</c>,
/// build WIN63-202610151745): the photographer's name <c>n</c>, shown with the date, and the time
/// <c>t</c> as <c>new Date(Number(t))</c>, milliseconds. The poster had no name, so the viewer showed
/// neither, and its time in seconds would have read as January 1970.
/// </summary>
public sealed class CameraPhotoPurchaseTests
{
    private const int PLAYER = 105;

    private readonly Fakes _fakes = new();
    private readonly string _storage = Path.Combine(
        Path.GetTempPath(),
        "turbo-tests",
        Path.GetRandomFileName()
    );

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_bought_photo_names_its_photographer_and_its_time_in_milliseconds()
    {
        var config = Options.Create(new CameraConfig { StoragePath = _storage, CreditPrice = 0 });
        var store = new CameraPhotoStore(config);
        var photo = await store.SavePhotoAsync(PLAYER, "{}", Ct);
        string? granted = null;

        _fakes.Handlers["TryGetDefinitionByName"] = _ => Poster();
        _fakes.Handlers["GetSummaryAsync"] = _ => Task.FromResult(Summary("agent5"));
        _fakes.Handlers["GrantFurnitureAsync"] = call =>
        {
            granted = (string?)call.Args[1];

            return Task.FromResult<FurnitureItemSnapshot?>(Item());
        };

        var handler = new PurchasePhotoMessageHandler(
            _fakes.Create<IGrainFactory>(),
            store,
            _fakes.Create<IFurnitureDefinitionProvider>(),
            config,
            NullLogger<PurchasePhotoMessageHandler>.Instance
        );

        await handler.HandleAsync(
            new PurchasePhotoMessage(),
            new MessageContext(_fakes.Create<ISessionContext>("session"), PLAYER, 0),
            Ct
        );

        _fakes
            .Log.Of("SendComposerAsync")
            .Select(x => x.Args[0])
            .OfType<CameraPurchaseOKMessageComposer>()
            .Should()
            .ContainSingle();

        granted.Should().NotBeNull();
        var state = JsonDocument
            .Parse(
                JsonDocument
                    .Parse(granted!)
                    .RootElement.GetProperty("stuff")
                    .GetProperty("Data")
                    .GetString()!
            )
            .RootElement;

        state.GetProperty("n").GetString().Should().Be("agent5");
        state.GetProperty("s").GetInt32().Should().Be(PLAYER);
        state.GetProperty("w").GetString().Should().Be(photo.Url);
        state
            .GetProperty("t")
            .GetInt64()
            .Should()
            .Be(new DateTimeOffset(photo.CreatedAt).ToUnixTimeMilliseconds());
    }

    private static FurnitureDefinitionSnapshot Poster() =>
        new()
        {
            Id = 99104,
            SpriteId = 4597,
            Name = "external_image_wallitem_poster_small",
            ProductType = ProductType.Wall,
            FurniCategory = FurnitureCategory.Default,
            LogicName = "default_wall",
            TotalStates = 0,
            Width = 1,
            Length = 1,
            StackHeight = Altitude.Zero,
            CanStack = false,
            CanWalk = false,
            CanSit = false,
            CanLay = false,
            CanRecycle = false,
            CanTrade = true,
            CanGroup = false,
            CanSell = true,
            UsagePolicy = FurnitureUsageType.Everybody,
            ExtraData = null,
        };

    private static FurnitureItemSnapshot Item() =>
        new()
        {
            ItemId = 1,
            SpriteId = 4597,
            OwnerId = PLAYER,
            OwnerName = "agent5",
            Definition = Poster(),
            StuffData = new LegacyStuffSnapshot { StuffBitmask = 0, Data = "" },
            ExtraData = string.Empty,
            SecondsToExpiration = -1,
            HasRentPeriodStarted = false,
            RoomId = -1,
        };

    private static PlayerSummarySnapshot Summary(string name) =>
        new()
        {
            PlayerId = PLAYER,
            Name = name,
            Motto = string.Empty,
            Figure = "hd-180-1",
            Gender = AvatarGenderType.Male,
            AchievementScore = 0,
            BadgesRank = 0,
            IsOnline = true,
            CreatedAt = DateTime.UnixEpoch,
            LastUpdated = DateTime.UnixEpoch,
            RespectPoints = 0,
            RespectsLeft = 0,
            PetRespectsLeft = 0,
            RespectReplenishesLeft = 0,
        };
}
