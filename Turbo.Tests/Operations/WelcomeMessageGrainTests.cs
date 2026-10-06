using FluentAssertions;
using Orleans;
using Turbo.Operations;
using Turbo.Primitives.Hotel.Grains;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Operations;

/// <summary>
/// The welcome message grain: what staff save is what every later login reads, across the grain
/// going away and coming back, and saving an empty message turns it off.
/// </summary>
public sealed class WelcomeMessageGrainTests : IDisposable
{
    private readonly SqliteDb _db = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task<IWelcomeMessageGrain> ActivateAsync()
    {
        var grain = GrainHarness.Create(
            typeof(OperationsModule).Assembly,
            "Turbo.Operations.Grains.WelcomeMessageGrain",
            new Fakes(),
            _db
        );
        await ((Grain)grain).OnActivateAsync(Ct);

        return (IWelcomeMessageGrain)grain;
    }

    [Fact]
    public async Task AHotelWithNoWelcomeMessage_HasNone()
    {
        var grain = await ActivateAsync();

        (await grain.GetMessageAsync(Ct)).Should().BeEmpty();
    }

    [Fact]
    public async Task ASavedMessage_IsTrimmed_AndReadAgainAfterTheGrainComesBack()
    {
        var grain = await ActivateAsync();

        var saved = await grain.SetMessageAsync("  Welcome!\r\nHave fun.  ", Ct);

        saved.Should().Be("Welcome!\nHave fun.");
        (await grain.GetMessageAsync(Ct)).Should().Be("Welcome!\nHave fun.");
        (await (await ActivateAsync()).GetMessageAsync(Ct)).Should().Be("Welcome!\nHave fun.");
    }

    [Fact]
    public async Task SavingAnEmptyMessage_TurnsItOff()
    {
        var grain = await ActivateAsync();
        await grain.SetMessageAsync("Welcome!", Ct);

        await grain.SetMessageAsync("   ", Ct);

        (await grain.GetMessageAsync(Ct)).Should().BeEmpty();
        (await (await ActivateAsync()).GetMessageAsync(Ct)).Should().BeEmpty();
    }
}
