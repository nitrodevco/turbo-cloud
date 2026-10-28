using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Database.Entities.Furniture;
using Turbo.Database.Entities.Players;
using Turbo.Database.Extensions;
using Turbo.Players;
using Turbo.Players.Configuration;
using Turbo.Players.Onboarding;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Messages.Incoming.Nux;
using Turbo.Primitives.Messages.Outgoing.Nux;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Pets.Providers;
using Turbo.Primitives.Pets.Snapshots;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Grains;
using Turbo.Primitives.Players.Onboarding.Snapshots;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Events.Player;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Players;

/// <summary>
/// The new user gift offer, as the official client draws and answers it (build
/// WIN63-202610201032): <c>NewUserExperienceGiftOfferParser</c> reads the steps (day, step, gifts;
/// each gift a thumbnail and products; each product a code and a text key),
/// <c>nux/NuxGiftSelectionView</c> takes one pick per step and <c>HabboNuxDialogs.onSendGetGifts</c>
/// sends them all in <c>NewUserExperienceGetGiftsMessageComposer</c>, whose count is the number of
/// ints that follow (three per pick).
/// </summary>
public sealed class NuxGiftTests : IDisposable
{
    private const int PLAYER = 1;
    private const int CHAIR = 501;

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();

    private readonly OnboardingConfig _config = new()
    {
        Gifts =
        [
            new()
            {
                Day = 1,
                Step = 1,
                Options =
                [
                    new()
                    {
                        Thumbnail = "nux/gift_chair.png",
                        Products = [new() { ProductCode = "chair_norja", Amount = 2 }],
                    },
                    new()
                    {
                        Products =
                        [
                            new()
                            {
                                ProductCode = "pet_cat",
                                LocalizationKey = "nux.gift.cat",
                                Pet = "1 0 ffffff",
                                PetName = "Kitty",
                            },
                        ],
                    },
                    new() { Products = [new() { ProductCode = "no_such_furni" }] },
                ],
            },
            new()
            {
                Day = 2,
                Step = 1,
                Options = [new() { Products = [new() { ProductCode = "chair_norja" }] }],
            },
        ],
    };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public NuxGiftTests()
    {
        _fakes.Handlers["TryGetDefinitionByName"] = call =>
            (string)call.Args[0]! == "chair_norja" ? Chair() : null;
        _fakes.Handlers["TryGetPalette"] = call =>
            (int)call.Args[0]! == 1 && (int)call.Args[1]! == 0
                ? new PetBreedSnapshot
                {
                    TypeId = 1,
                    BreedId = 7,
                    PaletteId = 0,
                    RarityLevel = 0,
                    Sellable = false,
                    Rare = false,
                    ColorTag = -1,
                }
                : null;
        _fakes.Handlers["CreatePetAsync"] = _ =>
            Task.FromResult<PetSnapshot?>(
                (PetSnapshot)RuntimeHelpers.GetUninitializedObject(typeof(PetSnapshot))
            );
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public void The_picks_are_read_as_the_client_counts_them_three_ints_each()
    {
        PacketHarness
            .Parse(
                PacketHarness.Incoming("NewUserExperienceGetGiftsMessageEvent"),
                PacketHarness.Payload(w => w.Int(6).Int(1).Int(1).Int(2).Int(2).Int(1).Int(0))
            )
            .Should()
            .BeOfType<NewUserExperienceGetGiftsMessage>()
            .Which.Selections.Should()
            .Equal(Pick(1, 1, 2), Pick(2, 1, 0));

        // A count larger than the packet holds reads only the picks that are there.
        PacketHarness
            .Parse(
                PacketHarness.Incoming("NewUserExperienceGetGiftsMessageEvent"),
                PacketHarness.Payload(w => w.Int(300).Int(1).Int(1).Int(0))
            )
            .Should()
            .BeOfType<NewUserExperienceGetGiftsMessage>()
            .Which.Selections.Should()
            .Equal(Pick(1, 1, 0));
    }

    [Fact]
    public async Task The_offer_is_written_as_the_client_reads_it()
    {
        Player(DateTime.UtcNow.AddHours(-2));
        var steps = await (await GrainAsync()).GetDueStepsAsync(Ct);
        var packet = PacketHarness.Encode(
            new NewUserExperienceGiftOfferEventMessageComposer { Steps = steps }
        );

        packet.PopInt().Should().Be(1, "only day 1 is due on the day of sign-up");
        packet.PopInt().Should().Be(1);
        packet.PopInt().Should().Be(1);
        packet.PopInt().Should().Be(3);
        packet.PopString().Should().Be("nux/gift_chair.png");
        packet.PopInt().Should().Be(1);
        packet.PopString().Should().Be("chair_norja");
        packet.PopString().Should().BeEmpty();
        packet.PopString().Should().BeEmpty();
        packet.PopInt().Should().Be(1);
        packet.PopString().Should().Be("pet_cat");
        packet.PopString().Should().Be("nux.gift.cat");
        packet.PopString().Should().BeEmpty();
        packet.PopInt().Should().Be(1);
        packet.PopString().Should().Be("no_such_furni");
        packet.PopString().Should().BeEmpty();
        packet.Remaining.Should().Be(0);
    }

    [Fact]
    public async Task A_pick_gives_the_gift_once_and_the_step_is_offered_no_more()
    {
        Player(DateTime.UtcNow.AddHours(-30));
        var grain = await GrainAsync();

        (await grain.GetDueStepsAsync(Ct))
            .Select(x => (x.DayIndex, x.StepIndex))
            .Should()
            .Equal((1, 1), (2, 1));

        (await grain.SelectAsync([Pick(1, 1, 0), Pick(2, 1, 0)], Ct)).Should().Be(2);
        (await grain.SelectAsync([Pick(1, 1, 1)], Ct)).Should().Be(0);

        _fakes
            .Log.Of("GrantFurnitureAsync")
            .Select(x => x.Args[0])
            .Should()
            .Equal(CHAIR, CHAIR, CHAIR);
        _fakes.Log.Of("CreatePetAsync").Should().BeEmpty();
        (await grain.GetDueStepsAsync(Ct)).Should().BeEmpty();

        // Taken for good: the grain coming back finds the steps picked.
        (await (await GrainAsync()).GetDueStepsAsync(Ct))
            .Should()
            .BeEmpty();
        await using var db = await _db.CreateDbContextAsync(Ct);

        db.PlayerProductClaims.Select(x => x.ClaimId)
            .ToList()
            .Should()
            .BeEquivalentTo("nux:1:1", "nux:2:1");
    }

    [Fact]
    public async Task A_pet_gift_gives_the_pet_by_its_figure()
    {
        Player(DateTime.UtcNow);

        (await (await GrainAsync()).SelectAsync([Pick(1, 1, 1)], Ct)).Should().Be(1);

        _fakes
            .Log.Of("CreatePetAsync")
            .Single()
            .Args.Take(6)
            .Should()
            .Equal("Kitty", 1, 0, 7, "ffffff", 0);
    }

    [Fact]
    public async Task Steps_not_due_and_gifts_not_there_give_nothing()
    {
        Player(DateTime.UtcNow);
        var grain = await GrainAsync();

        // Day 2 is not due yet, gift 5 does not exist, and the hotel has no "no_such_furni".
        (await grain.SelectAsync([Pick(2, 1, 0), Pick(1, 1, 5), Pick(1, 1, 2)], Ct))
            .Should()
            .Be(0);
        _fakes.Log.Of("GrantFurnitureAsync").Should().BeEmpty();
        (await grain.GetDueStepsAsync(Ct)).Should().ContainSingle("the step stays open");
    }

    [Fact]
    public async Task A_player_signed_up_longer_ago_than_GiftDays_is_offered_nothing()
    {
        Player(DateTime.UtcNow.AddDays(-8));
        var old = await GrainAsync();

        (await old.GetDueStepsAsync(Ct)).Should().BeEmpty("GiftDays is 7");
        (await old.SelectAsync([Pick(1, 1, 0)], Ct)).Should().Be(0);
    }

    [Fact]
    public async Task Entering_a_room_offers_the_due_steps_to_the_player()
    {
        Player(DateTime.UtcNow);
        _fakes.Instances[(typeof(IPlayerNuxGiftGrain), (object)(long)PLAYER)] = await GrainAsync();
        var listener = new NuxGiftRoomListener(
            Options.Create(_config),
            _fakes.Create<IGrainFactory>(),
            NullLogger<NuxGiftRoomListener>.Instance
        );

        await listener.OnRoomEventAsync(new PlayerEnterEvent { RoomId = 1, PlayerId = PLAYER }, Ct);

        _fakes
            .SentToPlayer<NewUserExperienceGiftOfferEventMessageComposer>(PLAYER)
            .Single()
            .Steps.Should()
            .ContainSingle()
            .Which.Options.Should()
            .HaveCount(3);
    }

    [Fact]
    public async Task No_gifts_configured_offers_nothing()
    {
        Player(DateTime.UtcNow);
        var listener = new NuxGiftRoomListener(
            Options.Create(new OnboardingConfig()),
            _fakes.Create<IGrainFactory>(),
            NullLogger<NuxGiftRoomListener>.Instance
        );

        await listener.OnRoomEventAsync(new PlayerEnterEvent { RoomId = 1, PlayerId = PLAYER }, Ct);

        _fakes.Log.Of("OfferAsync").Should().BeEmpty();
    }

    [Fact]
    public async Task NewUserExperienceGetGifts_hands_the_picks_to_the_players_grain()
    {
        Player(DateTime.UtcNow);
        var harness = new PacketHarness();
        var grain = await GrainAsync(harness.Fakes);

        harness.Fakes.Instances[(typeof(IPlayerNuxGiftGrain), (object)(long)PLAYER)] = grain;

        await harness.SendAsync(
            PacketHarness.Incoming("NewUserExperienceGetGiftsMessageEvent"),
            PacketHarness.Payload(w => w.Int(3).Int(1).Int(1).Int(0)),
            PLAYER
        );

        (await grain.GetDueStepsAsync(Ct)).Should().BeEmpty();
    }

    private static NuxGiftSelectionSnapshot Pick(int day, int step, int gift) =>
        new()
        {
            DayIndex = day,
            StepIndex = step,
            GiftIndex = gift,
        };

    private void Player(DateTime createdAt) =>
        _db.Insert(
            new PlayerEntity
            {
                Id = PLAYER,
                Name = "newbie",
                Figure = "hd-180-1",
                Gender = AvatarGenderType.Male,
                PlayerStatus = PlayerStatusType.Offline,
                CreatedAt = createdAt,
            }
        );

    private async Task<IPlayerNuxGiftGrain> GrainAsync(Fakes? fakes = null)
    {
        fakes ??= _fakes;

        if (!ReferenceEquals(fakes, _fakes))
        {
            foreach (var (name, handler) in _fakes.Handlers)
                fakes.Handlers[name] = handler;
        }

        var grain = GrainHarness.Create(
            typeof(PlayerModule).Assembly,
            "Turbo.Players.Grains.Onboarding.PlayerNuxGiftGrain",
            fakes,
            _db,
            playerId: PLAYER
        );
        RoomHarness.SetField(grain, "_config", _config);
        RoomHarness.SetField(grain, "_definitions", fakes.Create<IFurnitureDefinitionProvider>());
        RoomHarness.SetField(grain, "_petBreeds", fakes.Create<IPetBreedProvider>());
        await ((Grain)grain).OnActivateAsync(Ct);

        return (IPlayerNuxGiftGrain)grain;
    }

    private static FurnitureDefinitionSnapshot Chair() =>
        new FurnitureDefinitionEntity
        {
            Id = CHAIR,
            SpriteId = 30,
            Name = "chair_norja",
            ProductType = ProductType.Floor,
            FurniCategory = FurnitureCategory.Default,
            Logic = "default",
            Width = 1,
            Length = 1,
            StackHeight = 1,
            CanStack = true,
            CanWalk = false,
            CanSit = true,
            CanLay = false,
            CanRecycle = true,
            CanTrade = true,
            CanGroup = true,
            CanSell = true,
        }.ToSnapshot(0.01);
}
