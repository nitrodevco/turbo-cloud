using System;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Testing;
using Xunit;

namespace Skills.Tests;

/// <summary>
/// The plugin in a test hotel (turbo-cloud <c>docs/plugin-testing.md</c>): loaded from its sources
/// as a hotel loads it, then played as players play it. The hotel's API is
/// <see cref="FakeHotel"/> (<c>hotel.Fake</c>), its clock moves only when the test moves it.
/// </summary>
public sealed class SkillsTests : IAsyncLifetime
{
    // Saturday noon UTC: still the weekend in every time zone. The weekly job's times are the server's own.
    private static readonly DateTimeOffset Saturday = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    private readonly TestHotel _hotel = TestHotel.Create(Saturday);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await _hotel.LoadAsync<SkillsPlugin>(ct: Ct);

    public ValueTask DisposeAsync() => _hotel.DisposeAsync();

    [Fact]
    public async Task Training_from_the_window_moves_up_a_level_and_tells_the_player()
    {
        _hotel.Settings.Set("Skills:ExperiencePerTrain", 60);
        var alice = _hotel.Player("Alice");

        await alice.SendsAsync(new TrainMessage(), Ct);
        Assert.Equal(new SkillMessage(1, 60, 100), alice.Should().HaveReceived<SkillMessage>());
        _hotel.Notify.AssertNotAlerted(alice);

        await alice.SendsAsync(new TrainMessage(), Ct);
        Assert.Equal(new SkillMessage(2, 20, 100), alice.Should().HaveReceived<SkillMessage>());
        Assert.Equal(
            "You reached level 2!",
            _hotel.Notify.AssertAlerted(alice, SkillBook.LEVELLED).Text
        );
    }

    [Fact]
    public async Task Skills_says_your_level_to_every_player()
    {
        var bob = _hotel.Player("Bob").InRoom(1);

        await bob.SendsAsync(new TrainMessage(), Ct);
        await bob.SaysAsync(":skills", Ct);

        bob.Should().HaveBeenWhispered("You are level 1 (25/100 experience).");
    }

    [Fact]
    public async Task Everyone_is_back_at_level_one_after_monday_midnight()
    {
        var alice = _hotel.Player("Alice");

        await alice.SendsAsync(new TrainMessage(), Ct);
        await _hotel.Schedule.AdvanceAsync(TimeSpan.FromDays(3), Ct);
        await alice.SendsAsync(new GetSkillMessage(), Ct);

        Assert.Single(_hotel.Schedule.Ran);
        Assert.Equal(new SkillMessage(1, 0, 100), alice.Should().HaveReceived<SkillMessage>());
    }
}
