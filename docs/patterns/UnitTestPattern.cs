using Turbo.Primitives.Furniture;
using Xunit;

namespace Docs.Patterns;

// Reference-only sample, and nothing compiles it — check it against the test it mirrors,
// Turbo.Tests/Furniture/DimmerStatesTests.cs, before copying it.
//
// Tests live in Turbo.Tests (xunit v3, FluentAssertions 7.x; `dotnet test Turbo.Tests/Turbo.Tests.csproj`),
// mirroring the folder of the type under test (Turbo.Tests/Players/Permissions/ for
// Turbo.Primitives/Players/Permissions/), and the Code Quality workflow runs them.
//
// The shape: one public type per file, here the test class; its subject is something that
// already exists in the repository, so the sample cannot drift into testing an invented type.
// Pure functions over client-facing tables (the *States and *Colors classes under
// Turbo.Primitives/Furniture/) test directly. Grains, room modules and packet handlers are tested
// through Turbo.Tests/Support: RoomHarness / LiveRoomHarness build a room grain without a silo,
// GrainHarness any other grain, PacketHarness drives client bytes through the revision's parser,
// handler and serializer, and InMemoryDb / SqliteDb stand in for MySQL.
//
// Failure paths first: the rejections are what the client reacts badly to, and what a change
// to a validator is most likely to get wrong.

public class DimmerStatesTests
{
    [Theory]
    [InlineData("")] // empty
    [InlineData("000000")] // no leading hash
    [InlineData("#00000")] // one digit short
    [InlineData("#0000000")] // one digit long
    [InlineData("#00000G")] // not hexadecimal
    public void IsValidColor_RejectsAnythingElse(string color) =>
        Assert.False(DimmerStates.IsValidColor(color));

    [Theory]
    [InlineData("#000000")]
    [InlineData("#FFFFFF")]
    [InlineData("#0a1B2c")]
    public void IsValidColor_AcceptsSixDigitHex(string color) =>
        Assert.True(DimmerStates.IsValidColor(color));
}
