using System.Collections.Immutable;
using System.Linq;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Achievements.Enums;

namespace Turbo.Achievements;

/// <summary>Editable hotel defaults. Thresholds are policy, not purported official server data.</summary>
public static class AchievementDefaults
{
    public static ImmutableArray<AchievementSourceDefinition> Sources { get; } =
    [
        new(AchievementSources.ONLINE, 1, AchievementReducer.ElapsedSeconds),
        new(AchievementSources.LOGIN, 1, AchievementReducer.CalendarStreak),
        new(AchievementSources.ACCOUNT_AGE, 1, AchievementReducer.Maximum),
        new(AchievementSources.FIGURE, 1, AchievementReducer.Counter),
        new(AchievementSources.MOTTO, 1, AchievementReducer.Counter),
        new(AchievementSources.HC, 1, AchievementReducer.Maximum),
        new(AchievementSources.PURCHASED_HC, 1, AchievementReducer.Maximum),
        new(AchievementSources.VISIT, 1, AchievementReducer.Distinct),
        new(AchievementSources.FURNITURE, 1, AchievementReducer.Counter),
        new(AchievementSources.RESPECT_GIVEN, 1, AchievementReducer.Counter),
        new(AchievementSources.RESPECT_RECEIVED, 1, AchievementReducer.Counter),
        new(AchievementSources.PETS, 1, AchievementReducer.Maximum),
        new(AchievementSources.NUTRITION, 1, AchievementReducer.Counter),
        new(AchievementSources.PET_LEVEL, 1, AchievementReducer.Counter),
        new(AchievementSources.PET_RESPECT_GIVEN, 1, AchievementReducer.Counter),
        new(AchievementSources.PET_RESPECT_RECEIVED, 1, AchievementReducer.Counter),
        new(AchievementSources.FLOOR_HEIGHTS, 1, AchievementReducer.Maximum),
        new(AchievementSources.ROOM_RANK, 1, AchievementReducer.Rank),
    ];

    public static ImmutableArray<AchievementDefinition> Definitions { get; } =
    [
        Create(
            1001,
            "online",
            "identity",
            AchievementSources.ONLINE,
            AchievementReducer.ElapsedSeconds,
            60,
            [
                5,
                15,
                30,
                60,
                120,
                180,
                300,
                600,
                1200,
                1800,
                3000,
                6000,
                9000,
                12000,
                18000,
                24000,
                36000,
                48000,
                72000,
                100000,
            ],
            "ACH_AllTimeHotelPresence",
            true,
            0
        ),
        Create(
            1002,
            "login",
            "identity",
            AchievementSources.LOGIN,
            AchievementReducer.CalendarStreak,
            1,
            [1, 2, 3, 5, 7, 10, 14, 21, 30, 45, 60, 90, 120, 180, 270, 365, 540, 730, 1095, 1825],
            "ACH_Login",
            true,
            0
        ),
        Create(
            1003,
            "account-age",
            "identity",
            AchievementSources.ACCOUNT_AGE,
            AchievementReducer.Maximum,
            1,
            [1, 2, 3, 5, 7, 10, 14, 21, 30, 45, 60, 90, 120, 180, 270, 365, 540, 730, 1095, 1825],
            "ACH_RegistrationDuration",
            true,
            0
        ),
        Create(
            1004,
            "figure",
            "identity",
            AchievementSources.FIGURE,
            AchievementReducer.Counter,
            1,
            [1],
            "ACH_AvatarLooks",
            true,
            0
        ),
        Create(
            1005,
            "motto",
            "identity",
            AchievementSources.MOTTO,
            AchievementReducer.Counter,
            1,
            [1],
            "ACH_Motto",
            true,
            0
        ),
        Create(
            1006,
            "hc-duration",
            "identity",
            AchievementSources.HC,
            AchievementReducer.Maximum,
            86400,
            [1, 30, 90, 180, 365],
            "ACH_BasicClub",
            true,
            0
        ),
        Create(
            1007,
            "purchased-hc",
            "identity",
            AchievementSources.PURCHASED_HC,
            AchievementReducer.Maximum,
            1,
            [30, 60, 90, 180, 360],
            "ACH_HC",
            true,
            0
        ),
        Create(
            1008,
            "rooms-visited",
            "explore",
            AchievementSources.VISIT,
            AchievementReducer.Distinct,
            1,
            [
                1,
                2,
                3,
                5,
                10,
                15,
                20,
                30,
                50,
                75,
                100,
                150,
                200,
                300,
                500,
                750,
                1000,
                1500,
                2000,
                3000,
            ],
            "ACH_RoomEntry",
            true,
            0
        ),
        Create(
            1009,
            "furniture-use",
            "explore",
            AchievementSources.FURNITURE,
            AchievementReducer.Counter,
            1,
            [1],
            "ACH_HabboExplorer",
            true,
            0
        ),
        Create(
            1010,
            "respect-given",
            "social",
            AchievementSources.RESPECT_GIVEN,
            AchievementReducer.Counter,
            1,
            [1, 2, 3, 5, 10, 15, 20, 30, 50, 75],
            "ACH_RespectGiven",
            true,
            0
        ),
        Create(
            1011,
            "respect-received",
            "social",
            AchievementSources.RESPECT_RECEIVED,
            AchievementReducer.Counter,
            1,
            [1, 2, 3, 5, 10, 15, 20, 30, 50, 75],
            "ACH_RespectEarned",
            true,
            0
        ),
        Create(
            1012,
            "pets-owned",
            "pets",
            AchievementSources.PETS,
            AchievementReducer.Maximum,
            1,
            [1, 2, 3, 5, 10, 15, 20, 30, 50, 75],
            "ACH_PetLover",
            true,
            0
        ),
        Create(
            1013,
            "pet-nutrition",
            "pets",
            AchievementSources.NUTRITION,
            AchievementReducer.Counter,
            1,
            [10, 25, 50, 100, 250, 500, 1000, 2500, 5000, 10000],
            "ACH_PetFeeding",
            true,
            0
        ),
        Create(
            1014,
            "pet-levels",
            "pets",
            AchievementSources.PET_LEVEL,
            AchievementReducer.Counter,
            1,
            [1, 2, 3, 5, 10, 15, 20, 30, 50, 75],
            "ACH_PetLevelUp",
            true,
            0
        ),
        Create(
            1015,
            "pet-respect-given",
            "pets",
            AchievementSources.PET_RESPECT_GIVEN,
            AchievementReducer.Counter,
            1,
            [1, 2, 3, 5, 10, 15, 20, 30, 50, 75],
            "ACH_PetRespectGiver",
            true,
            0
        ),
        Create(
            1016,
            "pet-respect-received",
            "pets",
            AchievementSources.PET_RESPECT_RECEIVED,
            AchievementReducer.Counter,
            1,
            [1, 2, 3, 5, 10, 15, 20, 30, 50, 75],
            "ACH_PetRespectReceiver",
            true,
            0
        ),
        Create(
            1017,
            "floor-heights",
            "room_builder",
            AchievementSources.FLOOR_HEIGHTS,
            AchievementReducer.Maximum,
            1,
            [2, 3, 4, 5, 6],
            "ACH_HabboBuilder",
            false,
            0
        ),
        Create(
            1018,
            "room-rank",
            "room_builder",
            AchievementSources.ROOM_RANK,
            AchievementReducer.Rank,
            1,
            [2000, 1000, 500, 250, 100, 50, 10, 5, 1],
            "ACH_RoomRank",
            true,
            1
        ),
    ];

    private static AchievementDefinition Create(
        int id,
        string key,
        string category,
        string source,
        AchievementReducer reducer,
        int divisor,
        int[] requirements,
        string badgePrefix,
        bool enabled,
        int displayMethod
    ) =>
        new()
        {
            Id = id,
            Key = key,
            Revision = 1,
            Category = category,
            Source = source,
            Reducer = reducer,
            UnitDivisor = divisor,
            Order = id,
            Enabled = enabled,
            DisplayMethod = displayMethod,
            Levels = requirements
                .Select(
                    (requirement, index) =>
                        new AchievementLevelDefinition
                        {
                            Requirement = requirement,
                            BadgeCode = badgePrefix + (index + 1),
                            Score = 10,
                        }
                )
                .ToImmutableArray(),
        };
}
