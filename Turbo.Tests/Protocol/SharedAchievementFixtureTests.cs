using System.Text.Json;
using Turbo.Primitives.Achievements.Enums;
using Turbo.Primitives.Achievements.Snapshots;
using Turbo.Primitives.Messages.Outgoing.Inventory.Achievements;
using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Packets;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Protocol;

public sealed class SharedAchievementFixtureTests
{
    [Fact]
    public void StandardPacketsMatchThePortableNitroFixtures()
    {
        using var stream = typeof(SharedAchievementFixtureTests).Assembly.GetManifestResourceStream(
            "Turbo.Tests.Protocol.Fixtures.achievements.json"
        )!;
        using var document = JsonDocument.Parse(stream);
        var fixtures = document.RootElement;
        var first = Snapshot(fixtures.GetProperty("achievement"));
        var final = Snapshot(fixtures.GetProperty("finalAchievement"));
        Check(
            PacketHarness.Encode(new AchievementEventMessageComposer { Achievement = first }),
            fixtures.GetProperty("achievement")
        );
        Check(
            PacketHarness.Encode(new AchievementEventMessageComposer { Achievement = final }),
            fixtures.GetProperty("finalAchievement")
        );
        Check(
            PacketHarness.Encode(
                new AchievementsEventMessageComposer
                {
                    Achievements = [first],
                    DefaultCategory = "identity",
                }
            ),
            fixtures.GetProperty("list")
        );
        Check(
            PacketHarness.Encode(new AchievementsScoreEventMessageComposer { Score = 900 }),
            fixtures.GetProperty("score")
        );
        var n = fixtures.GetProperty("notification");
        Check(
            PacketHarness.Encode(
                new HabboAchievementNotificationMessageComposer
                {
                    Type = Int(n, 0),
                    Level = Int(n, 1),
                    BadgeId = Int(n, 2),
                    BadgeCode = String(n, 3),
                    PointsTotal = Int(n, 4),
                    LevelRewardPoints = Int(n, 5),
                    LevelRewardPointType = Int(n, 6),
                    BonusPoints = Int(n, 7),
                    AchievementId = Int(n, 8),
                    RemovedBadgeCode = String(n, 9),
                    Category = String(n, 10),
                    ShowDialogToUser = n[11][1].GetBoolean(),
                    OwnerCount = Int(n, 12),
                    BadgeRarityId = Int(n, 13),
                }
            ),
            n
        );
    }

    private static int Int(JsonElement fields, int index) => fields[index][1].GetInt32();

    private static string String(JsonElement fields, int index) => fields[index][1].GetString()!;

    private static AchievementSnapshot Snapshot(JsonElement f) =>
        new()
        {
            AchievementId = Int(f, 0),
            Level = Int(f, 1),
            BadgeId = String(f, 2),
            ScoreAtStartOfLevel = Int(f, 3),
            ScoreLimitTotal = Int(f, 4),
            LevelRewardPoints = Int(f, 5),
            LevelRewardPointType = Int(f, 6),
            CurrentPointsTotal = Int(f, 7),
            FinalLevel = f[8][1].GetBoolean(),
            Category = String(f, 9),
            SubCategory = String(f, 10),
            LevelCount = Int(f, 11),
            DisplayMethod = Int(f, 12),
            State = (AchievementState)Int(f, 13),
        };

    private static void Check(ClientPacket packet, JsonElement fixture)
    {
        foreach (var field in fixture.EnumerateArray())
            switch (field[0].GetString())
            {
                case "Int":
                    Assert.Equal(field[1].GetInt32(), packet.PopInt());
                    break;
                case "Short":
                    Assert.Equal(field[1].GetInt16(), packet.PopShort());
                    break;
                case "String":
                    Assert.Equal(field[1].GetString(), packet.PopString());
                    break;
                case "Boolean":
                    Assert.Equal(field[1].GetBoolean(), packet.PopBoolean());
                    break;
                default:
                    throw new InvalidOperationException("Unknown fixture type.");
            }
        Assert.True(packet.End);
    }
}
