using FluentAssertions;
using Turbo.Primitives.Furniture;
using Xunit;

namespace Docs.Patterns;

// Reference-only sample, and nothing compiles it.
//
// Tests live in Turbo.Tests (xunit v3, FluentAssertions 7.x), mirroring the folder of the type
// under test (Turbo.Tests/Players/Permissions/ for Turbo.Primitives/Players/Permissions/), and
// the Code Quality workflow runs them. It references Turbo.Primitives only; add a reference to a
// domain module when there is a pure function there worth testing, not to reach a grain.
//
// The shape: one public type per file, here the test class; its subject is something that
// already exists in the repository, so the sample cannot drift into testing an invented type.
// Pure functions over client-facing tables (the *States and *Colors classes under
// Turbo.Primitives/Furniture/) are what is worth unit testing — the rest of the server is
// grains, which need a test silo.
//
// Failure paths first: the rejections are what the client reacts badly to, and what a change
// to a validator is most likely to get wrong.

public class DimmerStatesTests
{
    [Theory]
    [InlineData("#000000")]
    [InlineData("#FFFFFF")]
    [InlineData("#0a1B2c")]
    public void IsValidColor_AcceptsSixDigitHex(string color)
    {
        DimmerStates.IsValidColor(color).Should().BeTrue();
    }

    [Theory]
    [InlineData("")] // empty
    [InlineData("000000")] // no leading hash
    [InlineData("#00000")] // one digit short
    [InlineData("#0000000")] // one digit long
    [InlineData("#00000G")] // not hexadecimal
    public void IsValidColor_RejectsAnythingElse(string color)
    {
        DimmerStates.IsValidColor(color).Should().BeFalse();
    }
}
