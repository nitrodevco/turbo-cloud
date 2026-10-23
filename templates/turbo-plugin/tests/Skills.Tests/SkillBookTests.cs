using System;
using Microsoft.Extensions.Options;
using Turbo.Primitives.Players;
using Xunit;

namespace Skills.Tests;

/// <summary>The plugin's own logic, without a server: a plain unit test.</summary>
public sealed class SkillBookTests
{
    private static SkillBook Book(int perTrain = 25, int perLevel = 100) =>
        new(
            new StaticOptions<SkillsSettings>(
                new SkillsSettings { ExperiencePerTrain = perTrain, ExperiencePerLevel = perLevel }
            )
        );

    [Fact]
    public void A_new_player_starts_at_level_one()
    {
        Assert.Equal(new Skill(1, 0, 100), Book().Of(new PlayerId(7)));
    }

    [Fact]
    public void Training_adds_experience_and_a_full_level_moves_up()
    {
        var book = Book(perTrain: 60);
        var player = new PlayerId(7);

        Assert.Equal(new Skill(1, 60, 100), book.Train(player));
        Assert.Equal(new Skill(2, 20, 100), book.Train(player));
        Assert.Equal(new Skill(1, 0, 100), book.Of(new PlayerId(8)));
    }

    private sealed class StaticOptions<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;

        public T Get(string? name) => value;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
