using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Admin.Api.Contracts;
using Turbo.Admin.Content;
using Turbo.Database.Entities.Pets;
using Turbo.Primitives.Bots.Snapshots;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Messages.Outgoing.Room.Pets;
using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Pets;
using Turbo.Primitives.Pets.Providers;
using Turbo.Primitives.Pets.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Providers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Admin;

/// <summary>
/// Pets and bots from the panel: a pet palette or line saved is what pets in rooms use at once,
/// as the providers are read again; a bot standing in a room is set through its room - renamed,
/// redressed, given lines - and the room sees it, or taken back to its owner's inventory; and
/// a pet standing in a room the same way - renamed, levelled, fed.
/// </summary>
public sealed class PetBotStaffTests : IDisposable
{
    private const int BOT = 31;
    private const int PET = 41;
    private const int OWNER = 9;

    private readonly SqliteDb _db = new();
    private readonly LiveRoomHarness _harness = new(10, 10);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public PetBotStaffTests()
    {
        RoomHarness.SetField(
            _harness.Room,
            "_avatarProvider",
            new RoomAvatarProvider(Options.Create(new BotConfig()))
        );
        _harness.Fakes.Handlers["LoadBotsByRoomIdAsync"] = _ =>
            Task.FromResult<IReadOnlyList<BotSnapshot>>([Bot()]);
        _harness.Fakes.Handlers["LoadPetsByRoomIdAsync"] = _ =>
            Task.FromResult<IReadOnlyList<PetSnapshot>>([Pet()]);
        _harness.Fakes.Handlers["ReturnPetAsync"] = _ => Task.FromResult(true);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task A_bot_set_by_staff_is_renamed_redressed_and_given_lines_where_it_stands()
    {
        var edit = new BotStaffEditSnapshot
        {
            Name = "  Greeter  ",
            Motto = "Welcome!",
            Figure = "hd-190-1",
            Gender = AvatarGenderType.Female,
            ChatText = "Hello\nHi there",
            AutoChat = true,
            ChatDelaySeconds = 1,
            MixSentences = false,
            FreeRoam = true,
            Dance = AvatarDanceType.None,
        };

        (await _harness.Room.StaffUpdateBotAsync(BOT, edit, Ct)).Should().BeTrue();

        _harness.Module<RoomBotModule>().TryGetBot(BOT, out var bot).Should().BeTrue();
        bot!.Name.Should().Be("Greeter");
        bot.Motto.Should().Be("Welcome!");
        bot.Figure.Should().Be("hd-190-1");
        bot.ChatDelaySeconds.Should()
            .Be(new BotConfig().ChatDelayMinSeconds, "held to the hotel's shortest delay");
        bot.FreeRoam.Should().BeTrue();

        SentToRoom<UsersMessageComposer>()
            .SelectMany(x => x.Avatars)
            .Should()
            .Contain(x => x.Name == "Greeter", "the room is sent the renamed bot");

        (await _harness.Room.StaffUpdateBotAsync(BOT, edit with { Name = "" }, Ct))
            .Should()
            .BeFalse();
        (await _harness.Room.StaffUpdateBotAsync(404, edit, Ct)).Should().BeFalse();
    }

    [Fact]
    public async Task A_bot_taken_out_by_staff_goes_back_to_its_owner()
    {
        (await _harness.Room.StaffPickupBotAsync(BOT, Ct)).Should().BeTrue();

        _harness.Module<RoomBotModule>().TryGetBot(BOT, out _).Should().BeFalse();
        _harness
            .Fakes.Log.Of("ReturnBotAsync")
            .Should()
            .ContainSingle()
            .Which.Key.Should()
            .Be((long)OWNER);
    }

    [Fact]
    public async Task A_pet_set_by_staff_is_renamed_levelled_and_fed_where_it_stands()
    {
        var edit = new PetStaffEditSnapshot
        {
            Name = "  Buddy  ",
            Level = 5,
            Energy = 500,
            HasBreedingPermission = true,
        };

        (await _harness.Room.StaffUpdatePetAsync(PET, edit, Ct)).Should().BeTrue();

        _harness.Module<RoomPetModule>().TryGetPet(PET, out var pet).Should().BeTrue();
        pet!.Name.Should().Be("Buddy");
        pet.Level.Should().Be(5);
        pet.Experience.Should()
            .Be(new PetConfig().LevelExperienceThresholds[3], "where level 5 starts");
        pet.Energy.Should().Be(new PetConfig().MaxEnergy, "held to the hotel's most");
        pet.Nutrition.Should().Be(40, "a value not given is left as it was");
        pet.HasBreedingPermission.Should().BeTrue();

        SentToRoom<UsersMessageComposer>()
            .SelectMany(x => x.Avatars)
            .Should()
            .Contain(x => x.Name == "Buddy", "the room is sent the renamed pet");
        SentToRoom<PetLevelUpdateMessageComposer>()
            .Should()
            .ContainSingle()
            .Which.Level.Should()
            .Be(5);

        (await _harness.Room.StaffUpdatePetAsync(PET, edit with { Name = "" }, Ct))
            .Should()
            .BeFalse();
        (await _harness.Room.StaffUpdatePetAsync(404, edit, Ct)).Should().BeFalse();
    }

    [Fact]
    public async Task A_pet_taken_out_by_staff_goes_back_to_its_owner()
    {
        (await _harness.Room.StaffPickupPetAsync(PET, Ct)).Should().BeTrue();

        _harness.Module<RoomPetModule>().TryGetPet(PET, out _).Should().BeFalse();
        _harness
            .Fakes.Log.Of("ReturnPetAsync")
            .Should()
            .ContainSingle()
            .Which.Key.Should()
            .Be((long)OWNER);
    }

    [Fact]
    public async Task A_pet_palette_or_line_saved_is_what_the_server_uses_at_once()
    {
        var breeds = new PetBreedProvider(_db, NullLogger<IPetBreedProvider>.Instance);
        var speech = new PetSpeechProvider(_db, NullLogger<IPetSpeechProvider>.Instance);
        var editor = new AdminPetEditor(_db, breeds, speech);

        await editor.SaveBreedAsync(0, new PetBreedRequest(1, 7, 2, 3, false, true, -1), Ct);
        await editor.SaveSpeechAsync(0, new PetSpeechRequest(1, "Meow!"), Ct);

        breeds.GetPalettes(1).Should().ContainSingle().Which.PaletteId.Should().Be(7);
        speech.GetLines(1).Should().Equal("Meow!");

        var line = (await editor.GetPetsAsync(Ct)).Speech.Single().Id;

        await editor.DeleteSpeechAsync(line, Ct);
        speech.GetLines(1).Should().BeEmpty();

        var twice = () =>
            editor.SaveBreedAsync(0, new PetBreedRequest(1, 7, 0, 0, null, null, null), Ct);

        await twice.Should().ThrowAsync<ArgumentException>();
    }

    private IEnumerable<T> SentToRoom<T>() =>
        _harness
            .Fakes.Log.Calls.SelectMany(x => x.Args)
            .OfType<RoomOutboundSnapshot>()
            .SelectMany(x => x.Composers)
            .OfType<T>();

    private static BotSnapshot Bot() =>
        new()
        {
            Id = BOT,
            OwnerId = new PlayerId(OWNER),
            OwnerName = "owner",
            RoomId = new RoomId(1),
            Name = "Bot",
            Motto = "",
            Figure = "hd-180-1",
            Gender = AvatarGenderType.Male,
            X = 3,
            Y = 3,
            Z = new Altitude(0),
            Rotation = Rotation.South,
            FreeRoam = false,
            ChatText = "",
            AutoChat = false,
            ChatDelaySeconds = 30,
            MixSentences = false,
            DanceType = AvatarDanceType.None,
        };

    private static PetSnapshot Pet() =>
        new()
        {
            Id = PET,
            OwnerId = new PlayerId(OWNER),
            OwnerName = "owner",
            RoomId = new RoomId(1),
            Name = "Rex",
            Figure = new PetFigureSnapshot
            {
                TypeId = PetTypes.DOG,
                PaletteId = 1,
                Color = "FFFFFF",
                BreedId = 0,
                CustomParts = [],
            },
            Level = 2,
            Experience = 0,
            Energy = 50,
            Nutrition = 40,
            Respect = 0,
            RarityLevel = 0,
            HasSaddle = false,
            AnyoneCanRide = false,
            HasBreedingPermission = false,
            X = 5,
            Y = 5,
            Z = new Altitude(0),
            Rotation = Rotation.South,
            CreatedAtUtc = DateTime.UtcNow,
            WateredAtUtc = DateTime.UtcNow,
            HarvestedAtUtc = null,
        };
}
