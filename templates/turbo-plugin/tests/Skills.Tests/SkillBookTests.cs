using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Turbo.Testing;
using Xunit;

namespace Skills.Tests;

/// <summary>
/// The plugin's own logic, without a server: the hotel it talks to is <see cref="FakeHotel"/>, the
/// server's hotel API in memory with the same rules (turbo-cloud <c>docs/hotel-api.md</c>).
/// </summary>
public sealed class SkillBookTests
{
    private readonly FakeHotel _hotel = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private SkillBook Book(int perTrain = 25, int perLevel = 100) =>
        new(
            new StaticOptions<SkillsSettings>(
                new SkillsSettings { ExperiencePerTrain = perTrain, ExperiencePerLevel = perLevel }
            ),
            _hotel
        );

    [Fact]
    public void A_new_player_starts_at_level_one()
    {
        var alice = _hotel.Players.Add("Alice");

        Assert.Equal(new Skill(1, 0, 100), Book().Of(alice));
    }

    [Fact]
    public async Task Training_adds_experience_and_a_full_level_moves_up_and_tells_the_player()
    {
        var book = Book(perTrain: 60);
        var alice = _hotel.Players.Add("Alice", online: true);
        var bob = _hotel.Players.Add("Bob");

        Assert.Equal(new Skill(1, 60, 100), await book.TrainAsync(alice, Ct));
        _hotel.Notify.AssertNotAlerted(alice);

        Assert.Equal(new Skill(2, 20, 100), await book.TrainAsync(alice, Ct));
        Assert.Equal(
            "You reached level 2!",
            _hotel.Notify.AssertAlerted(alice, SkillBook.LEVELLED).Text
        );
        Assert.Equal(new Skill(1, 0, 100), book.Of(bob));
    }

    private sealed class StaticOptions<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;

        public T Get(string? name) => value;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
