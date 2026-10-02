using Turbo.Achievements;
using Turbo.Primitives.Achievements.Enums;
using Xunit;

namespace Turbo.Tests.Achievements;

public class AchievementDefaultsTests
{
    [Fact]
    public void Defaults_ContainAllRequestedFamiliesWithIncreasingBadgeLevels()
    {
        var expectedPrefixes = new Dictionary<string, string>
        {
            ["online"] = "ACH_AllTimeHotelPresence",
            ["login"] = "ACH_Login",
            ["account-age"] = "ACH_RegistrationDuration",
            ["figure"] = "ACH_AvatarLooks",
            ["motto"] = "ACH_Motto",
            ["hc-duration"] = "ACH_BasicClub",
            ["purchased-hc"] = "ACH_HC",
            ["rooms-visited"] = "ACH_RoomEntry",
            ["furniture-use"] = "ACH_HabboExplorer",
            ["respect-given"] = "ACH_RespectGiven",
            ["respect-received"] = "ACH_RespectEarned",
            ["pets-owned"] = "ACH_PetLover",
            ["pet-nutrition"] = "ACH_PetFeeding",
            ["pet-levels"] = "ACH_PetLevelUp",
            ["pet-respect-given"] = "ACH_PetRespectGiver",
            ["pet-respect-received"] = "ACH_PetRespectReceiver",
            ["floor-heights"] = "ACH_HabboBuilder",
            ["room-rank"] = "ACH_RoomRank",
        };
        var definitions = AchievementDefaults.Definitions;

        Assert.Equal(18, definitions.Length);
        Assert.Equal(expectedPrefixes.Keys.Order(), definitions.Select(x => x.Key).Order());
        Assert.Equal(18, AchievementDefaults.Sources.Length);

        foreach (var definition in definitions)
        {
            var prefix = expectedPrefixes[definition.Key];
            Assert.NotEmpty(definition.Levels);
            Assert.All(
                definition.Levels.Select((level, index) => (level, index)),
                item => Assert.Equal($"{prefix}{item.index + 1}", item.level.BadgeCode)
            );

            var requirements = definition.Levels.Select(x => x.Requirement).ToArray();
            if (definition.Reducer == AchievementReducer.Rank)
            {
                Assert.Equal(requirements.OrderByDescending(x => x), requirements);
            }
            else
            {
                Assert.Equal(requirements.OrderBy(x => x), requirements);
            }
        }
    }
}
