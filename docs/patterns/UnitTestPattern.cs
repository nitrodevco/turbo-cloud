using FluentAssertions;
using Turbo.Primitives.Furniture;
using Xunit;

namespace Docs.Patterns;

// Reference-only sample, and nothing compiles it.
//
// Read this before copying it: the solution has no test project, and neither xunit nor
// FluentAssertions is in Directory.Packages.props. This file is the shape to write tests in
// once there is somewhere to put them, not something you can run today. Until then, the
// "verification of at least one edge/failure scenario" the PR expectations ask for is done by
// hand and described in the PR.
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
