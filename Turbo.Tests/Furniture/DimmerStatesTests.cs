using Turbo.Primitives.Furniture;
using Xunit;

namespace Turbo.Tests.Furniture;

/// <summary>A pure client-facing table: rejections first, as the client reacts worst to those.</summary>
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
