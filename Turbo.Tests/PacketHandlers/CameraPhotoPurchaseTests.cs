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
/// A bought photo is a poster whose state is only <c>{"t":&lt;ms&gt;,"id":"&lt;photo id&gt;"}</c>
/// (official client capture, 102 of 102 posters); the photo viewer asks the extra data service for
/// the rest (<c>ExternalImageWidget.readFurniJson</c> / <c>loadPhoto</c>, build WIN63-202610151745),
/// which answers in Habbo's long keys (the same capture, 100 of 100): the photographer's
/// <c>creator_name</c> and <c>creator_id</c>, shown with the date, and the photo's <c>time</c> as
/// <c>new Date(Number(time))</c>, milliseconds. The poster carried the whole photo in its own state.
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
    public async Task A_bought_photo_is_a_poster_of_its_id_and_time_whose_extra_data_names_its_photographer()
    {
        var config = Options.Create(new CameraConfig { StoragePath = _storage, CreditPrice = 0 });
        var store = new CameraPhotoStore(config);
        var photo = await store.SavePhotoAsync(PLAYER, 7, "{}", Ct);
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

        _fakes.Sent<CameraPurchaseOKMessageComposer>().Should().ContainSingle();

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

        var time = new DateTimeOffset(photo.CreatedAt).ToUnixTimeMilliseconds();

        state.EnumerateObject().Select(x => x.Name).Should().Equal("t", "id");
        state.GetProperty("t").GetInt64().Should().BeGreaterThanOrEqualTo(time);
        state.GetProperty("id").GetString().Should().Be(photo.Id);

        var file = new CameraPhotoFiles(config).GetExtraDataFile(photo.Id);

        file.Should().NotBeNull("the extra data service answers for the poster's id");
        var extraData = JsonDocument.Parse(await File.ReadAllTextAsync(file!, Ct)).RootElement;

        extraData.GetProperty("creator_name").GetString().Should().Be("agent5");
        extraData.GetProperty("creator_id").GetInt32().Should().Be(PLAYER);
        extraData.GetProperty("url").GetString().Should().Be(photo.Url);
        extraData.GetProperty("id").GetString().Should().Be(photo.Id);
        extraData.GetProperty("time").GetInt64().Should().Be(time);
        extraData.GetProperty("room_id").GetInt32().Should().Be(7);
        extraData.GetProperty("type").GetString().Should().Be("PHOTO");
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
