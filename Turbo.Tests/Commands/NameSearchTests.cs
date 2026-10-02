using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Database.Entities.Players;
using Turbo.Furniture;
using Turbo.Furniture.Configuration;
using Turbo.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Grains;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Commands;

/// <summary>The name searches a client's completion asks: players and furniture.</summary>
public class NameSearchTests : IDisposable
{
    private readonly SqliteDb _db = new();

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    private void Player(int id, string name) =>
        _db.Insert(
            new PlayerEntity
            {
                Id = id,
                Name = name,
                Figure = "hd-180-1",
                Gender = AvatarGenderType.Male,
                PlayerStatus = PlayerStatusType.Offline,
            }
        );

    private IPlayerDirectoryGrain Directory() =>
        (IPlayerDirectoryGrain)
            GrainHarness.Create(
                typeof(PlayerModule).Assembly,
                "Turbo.Players.Grains.PlayerDirectoryGrain",
                new Fakes(),
                _db
            );

    [Fact]
    public async Task Players_AreFoundByTheStartOfTheirName_IgnoringCase_SortedAndLimited()
    {
        Player(1, "Alice");
        Player(2, "albert");
        Player(3, "Alfred");
        Player(4, "Bob");
        Player(5, "Malice");

        var directory = Directory();

        (await directory.SearchNamesAsync("AL", 10, CancellationToken.None))
            .Should()
            .Equal("albert", "Alfred", "Alice");
        (await directory.SearchNamesAsync("al", 2, CancellationToken.None)).Should().HaveCount(2);
    }

    [Fact]
    public async Task APrefix_IsTakenLiterally_NotAsAPattern()
    {
        Player(1, "a_b");
        Player(2, "axb");
        Player(3, "100%");
        Player(4, "1000");

        var directory = Directory();

        (await directory.SearchNamesAsync("a_", 10, CancellationToken.None)).Should().Equal("a_b");
        (await directory.SearchNamesAsync("100%", 10, CancellationToken.None))
            .Should()
            .Equal("100%");
    }

    [Fact]
    public async Task AnEmptyPrefix_FindsNobody()
    {
        Player(1, "Alice");

        (await Directory().SearchNamesAsync("  ", 10, CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public void Furni_AreFoundByTheStartOfTheirName_IgnoringCase_SortedAndLimited()
    {
        var provider = new FurnitureDefinitionProvider(
            Options.Create(new FurnitureConfig()),
            _db,
            NullLogger<Turbo.Primitives.Furniture.Providers.IFurnitureDefinitionProvider>.Instance
        );
        RoomHarness.SetField(
            provider,
            "_sortedNames",
            new[] { "chair_basic", "Chair_Plasto", "rare_dragon", "throne" }
        );

        provider.FindNames("CHAIR", 10).Should().Equal("chair_basic", "Chair_Plasto");
        provider.FindNames("chair", 1).Should().Equal("chair_basic");
        provider.FindNames("zzz", 10).Should().BeEmpty();
        provider.FindNames("", 2).Should().Equal("chair_basic", "Chair_Plasto");
    }
}
