using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Database.Entities.Players;
using Turbo.Messages.Registry;
using Turbo.PacketHandlers.Camera;
using Turbo.Players;
using Turbo.Players.Configuration;
using Turbo.Primitives.Messages.Incoming.Camera;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Grains;
using Turbo.Primitives.Quests;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Quests;

/// <summary>
/// Reward track tasks of Habbo's Introduction track (official client capture 2026-10-12) counted
/// outside a room: <c>replenish_respect</c> (a respect replenish used) and <c>publish_picture</c>
/// (a photo published). Each counts when it went through, not when it was refused.
/// </summary>
public sealed class RewardTrackTaskHooksTests : IDisposable
{
    private const int PLAYER = 44;

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();
    private readonly string _storage = Path.Combine(
        Path.GetTempPath(),
        "turbo-tests",
        Path.GetRandomFileName()
    );

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _db.Dispose();

        if (Directory.Exists(_storage))
            Directory.Delete(_storage, recursive: true);
    }

    [Fact]
    public async Task A_respect_replenish_used_counts_and_one_with_none_left_does_not()
    {
        _db.Insert(
            new PlayerEntity
            {
                Id = PLAYER,
                Name = "replenisher",
                Figure = "hd-180-1",
                Gender = AvatarGenderType.Male,
                PlayerStatus = PlayerStatusType.Offline,
            }
        );
        var grain = GrainHarness.Create(
            typeof(PlayerModule).Assembly,
            "Turbo.Players.Grains.PlayerGrain",
            _fakes,
            _db,
            PLAYER
        );
        RoomHarness.SetField(
            grain,
            "_playerConfig",
            new PlayerConfig { RespectReplenishesPerDay = 1 }
        );

        (await ((IPlayerGrain)grain).ReplenishRespectAsync(Ct)).Should().BeTrue();
        (await ((IPlayerGrain)grain).ReplenishRespectAsync(Ct)).Should().BeFalse();

        Counted().Should().Equal(RewardTrackActionTypes.REPLENISH_RESPECT);
    }

    [Fact]
    public async Task A_photo_published_counts_and_one_refused_for_the_cooldown_does_not()
    {
        var config = Options.Create(
            new CameraConfig
            {
                StoragePath = _storage,
                PublishDucketPrice = 0,
                PublishCooldownSeconds = 60,
            }
        );
        var store = new CameraPhotoStore(config);
        await store.SavePhotoAsync(PLAYER, "{}", Ct);
        var handler = new PublishPhotoMessageHandler(
            _fakes.Create<IGrainFactory>(),
            store,
            config,
            NullLogger<PublishPhotoMessageHandler>.Instance
        );
        var ctx = new MessageContext(_fakes.Create<ISessionContext>("session"), PLAYER, 0);

        await handler.HandleAsync(new PublishPhotoMessage(), ctx, Ct);
        await handler.HandleAsync(new PublishPhotoMessage(), ctx, Ct);

        Counted().Should().Equal(RewardTrackActionTypes.PUBLISH_PICTURE);
    }

    private List<object?> Counted() =>
        [.. _fakes.Log.Of("RecordActionAsync").Select(x => x.Args[0])];
}
