using FluentAssertions;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Wired.Variables;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// <c>@level</c> (-210, user, always available, read-only) is the level the player's profile
/// shows, <c>ExtendedProfile.accountLevel</c>: on Habbo, inspection read @level 3 for a profile
/// saying Level 3 with achievement level 39 (official client capture 2026-10-14). Ours had the id
/// mapped and no variable behind it, so User Level conditions had nothing to read.
/// </summary>
public sealed class WiredUserLevelVariableTests
{
    private const int PLAYER_INDEX = 5;

    private readonly WiredRoom _room = new(8, 8);

    [Fact]
    public void Level_reads_the_account_level_of_the_players_summary()
    {
        var avatar = _room.Enter(PLAYER_INDEX, 1, 1);
        var level = Level();

        Read(level).Should().Be(PlayerAccountLevel.DEFAULT);

        avatar.UpdateWithPlayer(Summary(avatar.PlayerId, accountLevel: 3, achievementScore: 707));

        Read(level).Should().Be(3);
        level.GetVarSnapshot().VariableId.ToString().Should().Be("-210");
        level.GetVarSnapshot().Flags.Should().HaveFlag(WiredVariableFlags.AlwaysAvailable);
        level.GetVarSnapshot().Flags.Should().NotHaveFlag(WiredVariableFlags.CanWriteValue);
    }

    // Found by name as the other definition tests find Habbo's variables, so a server without
    // it fails here rather than not building.
    private WiredInternalVariable Level() =>
        typeof(WiredInternalVariable)
            .Assembly.GetTypes()
            .Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(WiredInternalVariable)))
            .Select(t => (WiredInternalVariable)Activator.CreateInstance(t, _room.Harness.Room)!)
            .Single(x =>
                x.GetVarSnapshot()
                    is { VariableName: "@level", TargetType: WiredVariableTargetType.User }
            );

    private static long? Read(WiredInternalVariable level) =>
        level.TryGetValue(
            new WiredVariableKey(
                level.GetVarSnapshot().VariableId,
                WiredVariableTargetType.User,
                PLAYER_INDEX
            ),
            out var value
        )
            ? value
            : null;

    private static PlayerSummarySnapshot Summary(
        PlayerId playerId,
        int accountLevel,
        int achievementScore
    ) =>
        new()
        {
            PlayerId = playerId,
            Name = "member",
            Motto = string.Empty,
            Figure = string.Empty,
            Gender = default,
            AchievementScore = achievementScore,
            BadgesRank = 0,
            IsOnline = true,
            CreatedAt = DateTime.UtcNow,
            LastUpdated = DateTime.UtcNow,
            RespectPoints = 0,
            RespectsLeft = 0,
            PetRespectsLeft = 0,
            RespectReplenishesLeft = 0,
            AccountLevel = accountLevel,
        };
}
