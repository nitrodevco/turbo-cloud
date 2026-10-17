using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Turbo.Database.Entities.Room;
using Turbo.Guilds;
using Turbo.Guilds.Configuration;
using Turbo.Primitives.Guilds.Enums;
using Turbo.Primitives.Guilds.Snapshots;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Players.Grains.Guilds;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Guilds;

/// <summary>
/// Habbo refuses a group to a player without Habbo Club: the wizard opens, but committing it is
/// answered with GuildEditFailed reason 2 (the "HC required" alert), nothing created and nothing
/// spent (official client capture 2026-10-17, 1/1). Turbo created it unless a hotel turned the
/// requirement on; it is now on unless a hotel turns it off.
/// </summary>
public sealed class GroupCreateClubRequiredTests
{
    private const int PLAYER = 102;
    private const int ROOM = 7;

    private readonly InMemoryDb _db = new();
    private readonly Fakes _fakes = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<IPlayerGuildGrain> OwnerOfARoomAsync()
    {
        await using (var dbCtx = await _db.CreateDbContextAsync(Ct))
        {
            dbCtx.Rooms.Add(
                new RoomEntity
                {
                    Id = ROOM,
                    Name = "homeroom",
                    PlayerEntityId = PLAYER,
                    RoomModelEntityId = 1,
                    DoorMode = RoomDoorModeType.Open,
                    UsersNow = 0,
                    PlayersMax = 25,
                    WallHeight = -1,
                    HideWalls = false,
                    ThicknessWall = RoomThicknessType.Normal,
                    ThicknessFloor = RoomThicknessType.Normal,
                    AllowBlocking = false,
                    AllowPets = true,
                    AllowPetsEat = true,
                    TradeType = RoomTradeModeType.Disabled,
                    MuteType = ModSettingType.Owner,
                    KickType = ModSettingType.Owner,
                    BanType = ModSettingType.Owner,
                    ChatFloodType = ChatFloodSensitivityType.Minimal,
                    PlayerEntity = null!,
                    RoomModelEntity = null!,
                }
            );
            await dbCtx.SaveChangesAsync(Ct);
        }

        var grain = GrainHarness.Create(
            typeof(GuildModule).Assembly,
            "Turbo.Guilds.Grains.PlayerGuildGrain",
            _fakes,
            _db,
            PLAYER
        );
        await ((Orleans.Grain)grain).OnActivateAsync(Ct);

        return (IPlayerGuildGrain)grain;
    }

    private static GuildCreationRequestSnapshot Request() =>
        new()
        {
            Name = "duo group",
            Description = string.Empty,
            RoomId = RoomId.Parse(ROOM),
            PrimaryColorId = 0,
            SecondaryColorId = 0,
            BadgeParts = [],
        };

    [Fact]
    public async Task A_player_without_club_is_refused_a_group_by_default()
    {
        _fakes.Handlers["HasActiveAsync"] = _ => Task.FromResult(false);
        var grain = await OwnerOfARoomAsync();

        var result = await grain.CreateGuildAsync(Request(), Ct);

        result.Failure.Should().Be(GuildCreationFailureType.ClubRequired);
        _fakes.Log.Of("HasActiveAsync").Should().ContainSingle();
    }

    [Fact]
    public void Club_is_required_by_default_and_in_the_shipped_settings()
    {
        new GuildConfig().CreationRequiresClub.Should().BeTrue();

        var path = Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "appsettings.json"
        );
        new ConfigurationBuilder()
            .AddJsonFile(path)
            .Build()
            .GetSection(GuildConfig.SECTION_NAME)
            .Get<GuildConfig>()!
            .CreationRequiresClub.Should()
            .BeTrue();
    }
}
