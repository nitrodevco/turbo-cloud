using FluentAssertions;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Wired.Storage;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Giving a variable again with "overwrite" restarts its creation time on official wired: a job
/// time tracker re-gives "on_duty" to start a new stretch and reads its creation time through
/// the "Time Utilities" add-on (baikal, "Habbo Wired: Time Tracking System", 2025).
/// </summary>
public sealed class WiredVariableGiveTimestampTests
{
    [Fact]
    public async Task An_overwriting_give_restarts_the_creation_time()
    {
        var store = new KeyValueStore();
        var key = new WiredVariableKey(new WiredVariableId(1), WiredVariableTargetType.User, 5);

        (await store.GiveValueAsync(key, new WiredVariableValue(1))).Should().BeTrue();
        store.TryGetTimestamps(key, out var firstCreated, out _).Should().BeTrue();

        await Task.Delay(20, TestContext.Current.CancellationToken);

        (await store.GiveValueAsync(key, new WiredVariableValue(1), replace: true))
            .Should()
            .BeTrue();
        store.TryGetTimestamps(key, out var secondCreated, out _).Should().BeTrue();

        secondCreated.Should().BeGreaterThan(firstCreated);
    }
}
