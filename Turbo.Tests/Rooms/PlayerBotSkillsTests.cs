using FluentAssertions;
using Microsoft.Extensions.Options;
using Turbo.Primitives.Bots.Snapshots;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Snapshots.Avatars;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Providers;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The skills a player's bot carries in <c>Users</c>. Habbo lists 1-5 and then 19-23 on every
/// player bot (official client capture, all 133 recorded player-bot entries, e.g.
/// <c>explore/2026-10-13T14-17-23-room-wait</c>); the client shows a menu button only for the
/// ones it knows (Flash <c>RentableBotMenuView.as:68-135</c>).
/// </summary>
public sealed class PlayerBotSkillsTests
{
    [Fact]
    public void A_placed_player_bot_lists_the_skills_Habbo_lists()
    {
        var bot = new RoomAvatarProvider(
            Options.Create(new BotConfig())
        ).CreateAvatarFromBotSnapshot(7, Bot());

        ((RoomRentableBotAvatarSnapshot)bot.GetSnapshot())
            .BotSkills.Should()
            .Equal(1, 2, 3, 4, 5, 19, 20, 21, 22, 23);
    }

    private static BotSnapshot Bot() =>
        new()
        {
            Id = 50,
            OwnerId = 1,
            OwnerName = "owner",
            RoomId = 1,
            Name = "Bot",
            Motto = string.Empty,
            Figure = "hd-180-1",
            Gender = AvatarGenderType.Male,
            X = 0,
            Y = 0,
            Z = Altitude.Zero,
            Rotation = Rotation.South,
            FreeRoam = false,
            ChatText = string.Empty,
            AutoChat = false,
            ChatDelaySeconds = 0,
            MixSentences = false,
            DanceType = AvatarDanceType.None,
        };
}
