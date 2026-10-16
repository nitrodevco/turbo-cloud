using System.Runtime.CompilerServices;
using FluentAssertions;
using Orleans;
using Turbo.Catalog;
using Turbo.Catalog.Configuration;
using Turbo.Database.Entities.Players;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Catalog.Grains;
using Turbo.Primitives.Messages.Incoming.Catalog;
using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Pets.Providers;
using Turbo.Primitives.Pets.Snapshots;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Catalog;

/// <summary>
/// Free product claims (<c>HasClaimedProduct</c> / <c>ClaimProduct</c>), as the official client's
/// cursed dragon adoption window uses them (build WIN63-202610151745,
/// <c>catalog/cursed_dragon_adoption/CursedDragonAdoptionController.as</c>): it asks whether the
/// claim was taken when it opens, then claims with the colour picked - 0 blue, 1 purple
/// (<c>CursedDragonAdoptionView.onBlueAdopt</c> / <c>onPurpleAdopt</c>) - and the pet it previews
/// is <c>"37 &lt;choice&gt; ffffff"</c> (<c>CursedDragonPreviewView.addPet</c>).
/// </summary>
public sealed class ProductClaimTests : IDisposable
{
    private const int PLAYER = 1;
    private const string CLAIM = "cursed_dragon_adoption";

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();

    private ProductClaimConfig _config = new()
    {
        Claims = new()
        {
            [CLAIM] = new()
            {
                Choices =
                [
                    new() { Pet = "37 0 ffffff", PetName = "Blue" },
                    new() { Pet = "37 1 ffffff" },
                ],
            },
        },
    };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ProductClaimTests()
    {
        _db.Insert(
            new PlayerEntity
            {
                Id = PLAYER,
                Name = "adopter",
                Figure = "hd-180-1",
                Gender = AvatarGenderType.Male,
                PlayerStatus = PlayerStatusType.Offline,
            }
        );
        _fakes.Handlers["TryGetPalette"] = call =>
            (int)call.Args[0]! == 37 && (int)call.Args[1]! is 0 or 1
                ? new PetBreedSnapshot
                {
                    TypeId = 37,
                    BreedId = 100 + (int)call.Args[1]!,
                    PaletteId = (int)call.Args[1]!,
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
    public async Task ClaimProduct_reads_the_choice_and_without_it_minus_one()
    {
        var header = PacketHarness.Incoming("ClaimProductMessageEvent");

        PacketHarness
            .Parse(header, PacketHarness.Payload(w => w.String(CLAIM).Int(1)))
            .Should()
            .Be(new ClaimProductMessage { ClaimId = CLAIM, Choice = 1 });
        PacketHarness
            .Parse(header, PacketHarness.Payload(w => w.String("chests")))
            .Should()
            .Be(new ClaimProductMessage { ClaimId = "chests", Choice = -1 });
    }

    [Fact]
    public async Task The_answers_are_written_as_the_client_reads_them()
    {
        PacketHarness
            .Encode(
                new HasClaimedProductResponseMessageComposer { ClaimId = CLAIM, HasClaimed = true }
            )
            .Should()
            .Match<ClientPacket>(p => p.PopString() == CLAIM && p.PopBoolean() && p.Remaining == 0);
        PacketHarness
            .Encode(
                new ClaimProductResultMessageComposer
                {
                    ClaimId = CLAIM,
                    Result = ProductClaimResult.AlreadyClaimed,
                }
            )
            .Should()
            .Match<ClientPacket>(p =>
                p.PopString() == CLAIM && p.PopInt() == 1 && p.Remaining == 0
            );
    }

    [Fact]
    public async Task The_purple_choice_gives_the_purple_dragon_once()
    {
        var grain = await GrainAsync();

        (await grain.HasClaimedAsync(CLAIM, Ct)).Should().BeFalse();
        (await grain.ClaimAsync(CLAIM, 1, Ct)).Should().Be(ProductClaimResult.Claimed);
        (await grain.HasClaimedAsync(CLAIM, Ct)).Should().BeTrue();
        (await grain.ClaimAsync(CLAIM, 0, Ct)).Should().Be(ProductClaimResult.AlreadyClaimed);

        _fakes
            .Log.Of("CreatePetAsync")
            .Single()
            .Args.Take(6)
            .Should()
            .Equal(ProductClaimChoice.DefaultPetName, 37, 1, 101, "ffffff", 0);
    }

    [Fact]
    public async Task A_claim_taken_before_is_known_after_the_grain_comes_back()
    {
        (await (await GrainAsync()).ClaimAsync(CLAIM, 0, Ct))
            .Should()
            .Be(ProductClaimResult.Claimed);

        var again = await GrainAsync();

        (await again.HasClaimedAsync(CLAIM, Ct)).Should().BeTrue();
        (await again.ClaimAsync(CLAIM, 1, Ct)).Should().Be(ProductClaimResult.AlreadyClaimed);
        _fakes.Log.Of("CreatePetAsync").Single().Args[0].Should().Be("Blue");
    }

    [Fact]
    public async Task Unknown_inactive_and_bad_choices_give_nothing_and_leave_the_claim_open()
    {
        _config.Claims["off"] = new() { Active = false, Choices = [new() { Pet = "37 0 ffffff" }] };
        _config.Claims["no_palette"] = new() { Choices = [new() { Pet = "37 9 ffffff" }] };
        var grain = await GrainAsync();

        (await grain.ClaimAsync("nothing", 0, Ct)).Should().Be(ProductClaimResult.NotFound);
        (await grain.ClaimAsync("off", 0, Ct)).Should().Be(ProductClaimResult.NotActive);
        (await grain.ClaimAsync(CLAIM, 2, Ct)).Should().Be(ProductClaimResult.Failed);
        // Two choices: a claim sent without one (the special items display's form) picks none.
        (await grain.ClaimAsync(CLAIM, -1, Ct))
            .Should()
            .Be(ProductClaimResult.Failed);
        (await grain.ClaimAsync("no_palette", -1, Ct)).Should().Be(ProductClaimResult.Failed);

        (await grain.HasClaimedAsync(CLAIM, Ct)).Should().BeFalse();
        (await grain.HasClaimedAsync("no_palette", Ct)).Should().BeFalse();
        _fakes.Log.Of("CreatePetAsync").Should().BeEmpty();
    }

    [Fact]
    public async Task HasClaimedProduct_and_ClaimProduct_are_answered_to_the_session()
    {
        var harness = new PacketHarness();
        var grain = await GrainAsync();

        harness.Fakes.Instances[(typeof(IPlayerProductClaimGrain), (object)(long)PLAYER)] = grain;

        var asked = await harness.SendAsync(
            PacketHarness.Incoming("HasClaimedProductMessageEvent"),
            PacketHarness.Payload(w => w.String(CLAIM)),
            PLAYER
        );

        asked.Should().ContainSingle();
        asked[0]
            .Header.Should()
            .Be(PacketHarness.Outgoing("HasClaimedProductResponseMessageComposer"));
        asked[0].PopString().Should().Be(CLAIM);
        asked[0].PopBoolean().Should().BeFalse();

        harness.Sent.Clear();
        var claimed = await harness.SendAsync(
            PacketHarness.Incoming("ClaimProductMessageEvent"),
            PacketHarness.Payload(w => w.String(CLAIM).Int(0)),
            PLAYER
        );

        claimed.Should().ContainSingle();
        claimed[0].Header.Should().Be(PacketHarness.Outgoing("ClaimProductResultMessageComposer"));
        claimed[0].PopString().Should().Be(CLAIM);
        claimed[0].PopInt().Should().Be((int)ProductClaimResult.Claimed);
    }

    private async Task<IPlayerProductClaimGrain> GrainAsync()
    {
        var grain = GrainHarness.Create(
            typeof(CatalogModule).Assembly,
            "Turbo.Catalog.Grains.PlayerProductClaimGrain",
            _fakes,
            _db,
            playerId: PLAYER
        );
        RoomHarness.SetField(grain, "_config", _config);
        RoomHarness.SetField(grain, "_petBreeds", _fakes.Create<IPetBreedProvider>());
        await ((Grain)grain).OnActivateAsync(Ct);

        return (IPlayerProductClaimGrain)grain;
    }
}
