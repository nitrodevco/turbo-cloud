using FluentAssertions;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Wired.Variables;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A variable's hash is what tells the client its copy of the variable is old
/// (<c>WiredVariablesSynchronizer</c> asks again only for a hash it does not hold). A text
/// connector is a value and its word, and only the words were hashed: "5=Sword" saved as
/// "6=Sword" kept the hash, so the client went on showing Sword for 5.
/// </summary>
public sealed class WiredVariableHashConnectorTests
{
    [Fact]
    public void A_connector_moved_to_another_value_changes_the_hash()
    {
        Hash(5, "Sword").Should().NotBe(Hash(6, "Sword"));
        Hash(5, "Sword").Should().Be(Hash(5, "Sword"));
    }

    private static WiredVariableHash Hash(int value, string word) =>
        WiredVariableHashBuilder.HashValues(
            "weapon",
            WiredAvailabilityType.UserActive,
            WiredVariableTargetType.User,
            WiredVariableFlags.HasValue,
            new Dictionary<WiredVariableValue, string> { [value] = word }
        );
}
