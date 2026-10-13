using FluentAssertions;
using Orleans;
using Turbo.Catalog;
using Turbo.Catalog.Configuration;
using Turbo.Database.Entities.Players;
using Turbo.Primitives.Action;
using Turbo.Primitives.Habbicons.Enums;
using Turbo.Primitives.Habbicons.Grains;
using Turbo.Primitives.Messages.Incoming.FriendList;
using Turbo.Primitives.Messages.Incoming.Habbicons;
using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Messages.Outgoing.Habbicons;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Wallet;
using Turbo.Primitives.Quests;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Catalog;

/// <summary>
/// Habbicons as the AS3 <c>HabbiconController</c> reads them (WIN63-202610131318): Habbo sends
/// <c>UserHabbicons</c> and <c>HabbiconShopData</c> unasked at login (official capture, 6 logins:
/// no Habbicons, and the duck, duck2, frank and toast collections). A buy or claim is answered
/// with each changed state and <c>PurchaseOK</c>, which closes the confirmation and refreshes the
/// shop (:532); a refusal with a purchase error (:543). States: 0 for sale, 1 claimable, 2 owned,
/// 3 favourited, 4 a reward not yet earned, 5 not sold (<c>HabbiconView.createEntryFromData</c>).
/// </summary>
public sealed class HabbiconTests : IDisposable
{
    private const int TOAST = 8;
    private const int TOAST_TOAST = 61;
    private const int TOAST_REWARD = 71;
    private const int DUCK_DUCK = 28;

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();

    public HabbiconTests()
    {
        _fakes.Handlers["TryDebitAsync"] = _ => Task.FromResult(WalletDebitResult.Success());
        _fakes.Handlers["ShowHabbiconAsync"] = _ => Task.FromResult(true);
        _db.Insert(
            new PlayerEntity
            {
                Id = 1,
                Name = "habbicons",
                Figure = "hd-180-1",
                Gender = AvatarGenderType.Male,
                PlayerStatus = PlayerStatusType.Offline,
            }
        );
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Login_sends_no_habbicons_then_habbos_shop_byte_for_byte()
    {
        await (await GrainAsync()).SendLoginAsync(Ct);

        var sent = Sent<IComposer>().ToList();
        sent.Select(x => x.GetType())
            .Should()
            .Equal(typeof(UserHabbiconsMessageComposer), typeof(HabbiconShopDataMessageComposer));
        Bytes(sent[0]).Should().Equal(Convert.FromHexString("0000000000000000"));
        Bytes(sent[1]).Should().Equal(Convert.FromHexString(OfficialShopData()));
    }

    [Fact]
    public async Task A_priced_habbicon_is_paid_for_owned_and_kept()
    {
        var grain = await GrainAsync();

        await grain.BuyAsync(TOAST_TOAST, Ct);

        ((List<WalletDebitRequest>)_fakes.Log.Of("TryDebitAsync").Single().Args[0]!)
            .Should()
            .Equal(
                new WalletDebitRequest { CurrencyKind = CurrencyKind.Credits, Amount = 5 },
                new WalletDebitRequest { CurrencyKind = CurrencyKind.ActivityPoints(5), Amount = 5 }
            );
        Sent<IComposer>()
            .Select(x => x.GetType())
            .Should()
            .Equal(
                typeof(UserHabbiconStatusChangedMessageComposer),
                typeof(PurchaseOKMessageComposer)
            );
        Single<UserHabbiconStatusChangedMessageComposer>()
            .Should()
            .Be(
                new UserHabbiconStatusChangedMessageComposer
                {
                    HabbiconId = TOAST_TOAST,
                    State = HabbiconState.Owned,
                }
            );

        _fakes.Log.Calls.Clear();
        await (await GrainAsync()).SendLoginAsync(Ct);

        Single<UserHabbiconsMessageComposer>()
            .Habbicons.Select(x => (x.HabbiconId, x.State))
            .Should()
            .Equal((TOAST_TOAST, HabbiconState.Owned));
        Single<HabbiconShopDataMessageComposer>()
            .Collections.Single(x => x.CollectionId == TOAST)
            .Habbicons.Single(x => x.HabbiconId == TOAST_TOAST)
            .State.Should()
            .Be(HabbiconState.Owned);
    }

    [Fact]
    public async Task An_unpriced_or_owned_habbicon_is_refused_without_charging()
    {
        var grain = await GrainAsync();
        await grain.BuyAsync(TOAST_TOAST, Ct);
        _fakes.Log.Calls.Clear();

        await grain.BuyAsync(DUCK_DUCK, Ct);
        await grain.BuyAsync(TOAST_TOAST, Ct);
        await grain.BuyAsync(9999, Ct);

        _fakes.Log.Of("TryDebitAsync").Should().BeEmpty();
        Sent<IComposer>()
            .Should()
            .HaveCount(3)
            .And.AllBeOfType<PurchaseNotAllowedMessageComposer>();
    }

    [Fact]
    public async Task Too_few_credits_is_a_purchase_error_and_gives_nothing()
    {
        _fakes.Handlers["TryDebitAsync"] = _ =>
            Task.FromResult(
                WalletDebitResult.InsufficientBalance(
                    new WalletDebitFailure { CurrencyKind = CurrencyKind.Credits, Amount = 5 }
                )
            );
        var grain = await GrainAsync();

        await grain.BuyAsync(TOAST_TOAST, Ct);

        Single<PurchaseErrorMessageComposer>()
            .ErrorCode.Should()
            .Be(Turbo.Primitives.Catalog.Enums.CatalogPurchaseErrorType.NotEnoughCredits);
        Sent<UserHabbiconStatusChangedMessageComposer>().Should().BeEmpty();
        (await grain.CanUseAsync(TOAST_TOAST, Ct)).Should().BeFalse();
    }

    [Fact]
    public async Task The_set_completes_the_collection_and_its_reward_is_claimed()
    {
        var grain = await GrainAsync();

        await grain.BuyCollectionAsync(TOAST, Ct);

        ((List<WalletDebitRequest>)_fakes.Log.Of("TryDebitAsync").Single().Args[0]!)
            .Select(x => x.Amount)
            .Should()
            .Equal(50, 50);
        Sent<UserHabbiconStatusChangedMessageComposer>()
            .Select(x => (x.HabbiconId, x.State))
            .Should()
            .Equal([
                .. Enumerable.Range(61, 10).Select(id => (id, HabbiconState.Owned)),
                (TOAST_REWARD, HabbiconState.Claimable),
            ]);
        Sent<PurchaseOKMessageComposer>().Should().ContainSingle();

        _fakes.Log.Calls.Clear();
        await grain.BuyCollectionAsync(TOAST, Ct);
        Single<PurchaseNotAllowedMessageComposer>();

        _fakes.Log.Calls.Clear();
        await grain.ClaimAsync(TOAST_REWARD, Ct);
        Single<UserHabbiconStatusChangedMessageComposer>().State.Should().Be(HabbiconState.Owned);
        Single<PurchaseOKMessageComposer>();

        _fakes.Log.Calls.Clear();
        await grain.SendShopDataAsync(Ct);
        var toast = Single<HabbiconShopDataMessageComposer>()
            .Collections.Single(x => x.CollectionId == TOAST);
        (toast.Completed, toast.RewardState).Should().Be((true, HabbiconState.Owned));
    }

    [Fact]
    public async Task A_reward_not_earned_cannot_be_claimed()
    {
        var grain = await GrainAsync();

        await grain.ClaimAsync(TOAST_REWARD, Ct);

        Single<PurchaseNotAllowedMessageComposer>();
        (await grain.CanUseAsync(TOAST_REWARD, Ct)).Should().BeFalse();
    }

    [Fact]
    public async Task Only_an_owned_habbicon_is_favourited_and_unfavourited()
    {
        var grain = await GrainAsync();
        await grain.SetFavouriteAsync(TOAST_TOAST, true, Ct);
        Sent<IComposer>().Should().BeEmpty();

        await grain.BuyAsync(TOAST_TOAST, Ct);
        _fakes.Log.Calls.Clear();
        await grain.SetFavouriteAsync(TOAST_TOAST, true, Ct);
        await grain.SetFavouriteAsync(TOAST_TOAST, false, Ct);

        Sent<UserHabbiconStatusChangedMessageComposer>()
            .Select(x => x.State)
            .Should()
            .Equal(HabbiconState.Favourite, HabbiconState.Owned);
    }

    [Fact]
    public async Task An_owned_habbicon_is_shown_in_the_room_counted_and_kept_recent()
    {
        var grain = await GrainAsync();
        var ctx = ActionContext.CreateForPlayer(1, 7);

        (await grain.TriggerAsync(ctx, TOAST_TOAST, Ct)).Should().BeFalse("it is not owned");
        _fakes.Log.Of("ShowHabbiconAsync").Should().BeEmpty();

        await grain.BuyAsync(TOAST_TOAST, Ct);
        (await grain.TriggerAsync(ctx, TOAST_TOAST, Ct)).Should().BeTrue();

        _fakes.Log.Of("ShowHabbiconAsync").Single().Args[1].Should().Be(TOAST_TOAST);
        _fakes
            .Log.Of("RecordActionAsync")
            .Single()
            .Args[0]
            .Should()
            .Be(RewardTrackActionTypes.USE_HABBICON);

        _fakes.Log.Calls.Clear();
        await (await GrainAsync()).SendLoginAsync(Ct);
        Single<UserHabbiconsMessageComposer>().RecentHabbiconIds.Should().Equal(TOAST_TOAST);
    }

    [Fact]
    public void The_client_messages_are_read_as_the_flash_client_writes_them()
    {
        PacketHarness
            .Parse(
                PacketHarness.Incoming("BuyHabbiconMessageEvent"),
                PacketHarness.Payload(w => w.Int(61))
            )
            .Should()
            .Be(new BuyHabbiconMessage { HabbiconId = 61 });
        PacketHarness
            .Parse(
                PacketHarness.Incoming("BuyHabbiconCollectionMessageEvent"),
                PacketHarness.Payload(w => w.Int(8))
            )
            .Should()
            .Be(new BuyHabbiconCollectionMessage { CollectionId = 8 });
        PacketHarness
            .Parse(
                PacketHarness.Incoming("SendHabbiconMessageEvent"),
                PacketHarness.Payload(w => w.Int(2).Int(61).Int(3))
            )
            .Should()
            .Be(
                new SendHabbiconMessage
                {
                    ChatId = 2,
                    HabbiconId = 61,
                    ConfirmationId = 3,
                }
            );
        Bytes(new RoomUseHabbiconMessageComposer { RoomIndex = 4, HabbiconId = 61 })
            .Should()
            .Equal(Convert.FromHexString("000000040000003D"));
        Bytes(
                new UserHabbiconStatusChangedMessageComposer
                {
                    HabbiconId = 61,
                    State = HabbiconState.Favourite,
                }
            )
            .Should()
            .Equal(Convert.FromHexString("0000003D00000003"));
    }

    private async Task<IPlayerHabbiconGrain> GrainAsync()
    {
        var grain = GrainHarness.Create(
            typeof(CatalogModule).Assembly,
            "Turbo.Catalog.Grains.PlayerHabbiconGrain",
            _fakes,
            _db,
            playerId: 1
        );
        RoomHarness.SetField(
            grain,
            "_config",
            new HabbiconConfig { Collections = HabboHabbicons.Collections() }
        );
        RoomHarness.SetField(grain, "_time", TimeProvider.System);
        await ((Grain)grain).OnActivateAsync(Ct);

        return (IPlayerHabbiconGrain)grain;
    }

    private static string OfficialShopData()
    {
        using var stream =
            typeof(HabbiconTests).Assembly.GetManifestResourceStream(
                "Catalog.Fixtures.hhus-habbicon-shop-data-2026-10-12.hex"
            ) ?? throw new InvalidOperationException("The official HabbiconShopData is missing.");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd().Trim();
    }

    private static byte[] Bytes(IComposer composer)
    {
        var packet = PacketHarness.Encode(composer);

        return packet.PopBytes(packet.Remaining);
    }

    private IEnumerable<T> Sent<T>()
        where T : IComposer =>
        _fakes
            .Log.Of("SendComposerAsync")
            .SelectMany(x =>
                x.Args[0] switch
                {
                    IReadOnlyList<IComposer> many => many,
                    IComposer one => [one],
                    _ => [],
                }
            )
            .OfType<T>();

    private T Single<T>()
        where T : IComposer => Sent<T>().Should().ContainSingle().Subject;
}
