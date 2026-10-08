using System.Globalization;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Orleans;
using Turbo.Database.Entities.Furniture;
using Turbo.Database.Entities.Players;
using Turbo.Furniture;
using Turbo.Furniture.Providers;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Sound;
using Turbo.Primitives.Sound.Grains;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Furniture;

/// <summary>
/// The song directory: what staff save is what every client and jukebox reads, across the grain
/// going away and coming back; a song it cannot store or play is refused with why; an official
/// song is found by its catalog code; and a song a disk carries is counted and kept.
/// </summary>
public sealed class SongDirectoryGrainTests : IDisposable
{
    private const int OWNER = 1;
    private const int DISK_DEFINITION = 10;

    private readonly SqliteDb _db = new();

    public SongDirectoryGrainTests()
    {
        _db.Insert(
            new PlayerEntity
            {
                Id = OWNER,
                Name = "owner",
                Figure = "hd-180-1",
                Gender = AvatarGenderType.Male,
                PlayerStatus = PlayerStatusType.Offline,
            }
        );
        _db.Insert(
            new FurnitureDefinitionEntity
            {
                Id = DISK_DEFINITION,
                SpriteId = 2607,
                Name = "song_disk",
                ProductType = ProductType.Floor,
                FurniCategory = FurnitureCategory.TraxSong,
                Logic = SongDisks.LOGIC,
                Width = 1,
                Length = 1,
                StackHeight = 0,
                CanStack = true,
                CanWalk = false,
                CanSit = false,
                CanLay = false,
                CanRecycle = false,
                CanTrade = true,
                CanGroup = false,
                CanSell = true,
            }
        );
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task ASavedSong_IsReadByIdAgainAfterTheGrainComesBack()
    {
        var created = await (await ActivateAsync()).CreateSongAsync(Draft("Habbo Theme"), Ct);

        created.Saved.Should().BeTrue();

        var songs = await (await ActivateAsync()).GetSongsAsync([created.Song!.Id, 999], Ct);

        songs.Should().ContainSingle();
        songs[0].Name.Should().Be("Habbo Theme");
        songs[0].Author.Should().Be("Staff");
        songs[0].Track.Should().Be("1:0,4;2:0,4");
        songs[0].LengthSeconds.Should().Be(120);
        songs[0].IsOfficial.Should().BeTrue();
    }

    [Theory]
    [InlineData("", 120, null)] // no name
    [InlineData("Song", 0, null)] // lasts no time
    [InlineData("Song", 120, "7up")] // a code the catalog would read as a song id
    public async Task ASongThatCannotBeStoredOrPlayed_IsRefused(
        string name,
        int lengthSeconds,
        string? code
    )
    {
        var grain = await ActivateAsync();

        var result = await grain.CreateSongAsync(
            Draft(name) with
            {
                LengthSeconds = lengthSeconds,
                Code = code,
            },
            Ct
        );

        result.Saved.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
        (await grain.GetAllSongsAsync(Ct)).Should().BeEmpty();
    }

    [Fact]
    public async Task AnOfficialSong_IsFoundByItsCode_InAnyCase_AndNoTwoSongsShareOne()
    {
        var grain = await ActivateAsync();
        var song = (
            await grain.CreateSongAsync(Draft("Theme") with { Code = "theme_1" }, Ct)
        ).Song!;

        (await grain.GetSongIdByCodeAsync("THEME_1", Ct)).Should().Be(song.Id);
        (await grain.GetSongIdByCodeAsync("nothing", Ct)).Should().BeNull();

        var second = await grain.CreateSongAsync(Draft("Other") with { Code = "Theme_1" }, Ct);

        second.Saved.Should().BeFalse();
    }

    [Fact]
    public async Task AnEditReplacesTheSong_AndItsCode()
    {
        var grain = await ActivateAsync();
        var song = (await grain.CreateSongAsync(Draft("Theme") with { Code = "old" }, Ct)).Song!;

        var edited = await grain.UpdateSongAsync(
            song.Id,
            Draft("Theme (remix)") with
            {
                Code = "new",
                LengthSeconds = 90,
            },
            Ct
        );

        edited.Saved.Should().BeTrue();
        (await grain.GetSongIdByCodeAsync("old", Ct)).Should().BeNull();
        (await grain.GetSongIdByCodeAsync("new", Ct)).Should().Be(song.Id);
        var read = (await (await ActivateAsync()).GetSongsAsync([song.Id], Ct)).Single();
        read.Name.Should().Be("Theme (remix)");
        read.LengthSeconds.Should().Be(90);
        (await grain.UpdateSongAsync(404, Draft("Gone"), Ct)).NotFound.Should().BeTrue();
    }

    [Fact]
    public async Task ASongADiskCarries_IsCountedAndCannotBeDeleted_OneNoDiskCarriesCan()
    {
        var grain = await ActivateAsync();
        var carried = (await grain.CreateSongAsync(Draft("On a disk"), Ct)).Song!;
        var loose = (await grain.CreateSongAsync(Draft("On no disk"), Ct)).Song!;
        _db.Insert(Disk(100, carried.Id));
        _db.Insert(Disk(101, carried.Id));

        (await grain.CountDisksAsync(Ct))
            .Should()
            .BeEquivalentTo(new Dictionary<int, int> { [carried.Id] = 2 });

        (await grain.DeleteSongAsync(carried.Id, Ct)).Saved.Should().BeFalse();
        (await grain.DeleteSongAsync(loose.Id, Ct)).Saved.Should().BeTrue();

        (await (await ActivateAsync()).GetAllSongsAsync(Ct))
            .Select(x => x.Id)
            .Should()
            .Equal(carried.Id);
    }

    private async Task<ISongDirectoryGrain> ActivateAsync()
    {
        var grain = GrainHarness.Create(
            typeof(FurnitureModule).Assembly,
            "Turbo.Furniture.Grains.SongDirectoryGrain",
            new Fakes(),
            _db
        );

        RoomHarness.SetField(
            grain,
            "_stuffDataFactory",
            new StuffDataFactory(NullLogger<IStuffDataFactory>.Instance)
        );

        await ((Grain)grain).OnActivateAsync(Ct);

        return (ISongDirectoryGrain)grain;
    }

    private static SongDraft Draft(string name) =>
        new()
        {
            Name = name,
            Author = " Staff ",
            Track = "1:0,4;2:0,4",
            LengthSeconds = 120,
            IsOfficial = true,
        };

    private static FurnitureEntity Disk(int id, int songId) =>
        new()
        {
            Id = id,
            PlayerEntityId = OWNER,
            FurnitureDefinitionEntityId = DISK_DEFINITION,
            ExtraData = ProductStuffData.ExtraData(songId.ToString(CultureInfo.InvariantCulture)),
        };
}
