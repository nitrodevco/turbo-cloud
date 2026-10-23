using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Extensions.Options;
using Turbo.Primitives.Players;

namespace Skills;

/// <summary>A player's skill: their level, the experience in it and what the level needs.</summary>
public sealed record Skill(int Level, int Experience, int Needed);

/// <summary>
/// Each player's experience. Kept in memory to keep the template small: a real plugin keeps it in
/// its own table (turbo-cloud <c>docs/database.md</c>).
/// </summary>
public sealed class SkillBook(IOptionsMonitor<SkillsSettings> settings)
{
    private readonly ConcurrentDictionary<PlayerId, int> _experience = new();

    public Skill Of(PlayerId player) => ToSkill(_experience.GetValueOrDefault(player));

    public Skill Train(PlayerId player) =>
        ToSkill(
            _experience.AddOrUpdate(
                player,
                settings.CurrentValue.ExperiencePerTrain,
                (_, current) => current + settings.CurrentValue.ExperiencePerTrain
            )
        );

    private Skill ToSkill(int total)
    {
        var perLevel = settings.CurrentValue.ExperiencePerLevel;

        return new(1 + (total / perLevel), total % perLevel, perLevel);
    }
}
