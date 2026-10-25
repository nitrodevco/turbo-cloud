using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Turbo.Contracts.Hotel;
using Turbo.Primitives.Players;

namespace Skills;

/// <summary>A player's skill: their level, the experience in it and what the level needs.</summary>
public sealed record Skill(int Level, int Experience, int Needed);

/// <summary>
/// Each player's experience. Kept in memory to keep the template small: a real plugin keeps it in
/// its own table (turbo-cloud <c>docs/database.md</c>). A level reached is told to the player
/// through the hotel API (turbo-cloud <c>docs/hotel-api.md</c>), which the tests fake.
/// </summary>
public sealed class SkillBook(IOptionsMonitor<SkillsSettings> settings, IHotel hotel)
{
    public const string LEVELLED = "skills.levelled";

    private readonly ConcurrentDictionary<PlayerId, int> _experience = new();

    public Skill Of(PlayerId player) => ToSkill(_experience.GetValueOrDefault(player));

    public async Task<Skill> TrainAsync(PlayerId player, CancellationToken ct)
    {
        var before = Of(player);
        var after = ToSkill(
            _experience.AddOrUpdate(
                player,
                settings.CurrentValue.ExperiencePerTrain,
                (_, current) => current + settings.CurrentValue.ExperiencePerTrain
            )
        );

        if (after.Level > before.Level)
            await hotel
                .Notify.AlertAsync(
                    player,
                    LEVELLED,
                    "You reached level %0%!",
                    [after.Level.ToString()],
                    ct
                )
                .ConfigureAwait(false);

        return after;
    }

    private Skill ToSkill(int total)
    {
        var perLevel = settings.CurrentValue.ExperiencePerLevel;

        return new(1 + (total / perLevel), total % perLevel, perLevel);
    }
}
