using System;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Turbo.Commands;
using Turbo.Primitives.Commands;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Commands;

public class CommandRegistryTests
{
    private readonly CapturingLogger<ICommandRegistryProvider> _log = new();

    private CommandRegistryProvider NewProvider() => new(_log);

    [Fact]
    public void Register_ClashingName_FailsAndRegistersNothingOfTheBatch()
    {
        var provider = NewProvider();
        provider.Register([new BootCommand()]);

        var act = () => provider.Register([new HelloCommand(), new EjectCommand()]);

        act.Should().Throw<InvalidOperationException>().WithMessage("*'eject'*already taken*");
        provider.Current.TryFind("hello", out _).Should().BeFalse();
        provider.Current.TryFind("boot", out _).Should().BeTrue();
    }

    [Fact]
    public void Register_TheSameNameTwiceInOneBatch_Fails()
    {
        var provider = NewProvider();

        var act = () => provider.Register([new BootCommand(), new EjectCommand()]);

        act.Should().Throw<InvalidOperationException>();
        provider.Current.Commands.Should().BeEmpty();
    }

    [Fact]
    public void Register_CommandWithoutAPermission_IsRefused()
    {
        var act = () => NewProvider().Register([new UngatedCommand()]);

        act.Should().Throw<InvalidOperationException>().WithMessage("*RequiresPermission*");
    }

    [Fact]
    public void Register_NameTheAirClientSwallows_WarnsButRegisters()
    {
        var provider = NewProvider();

        provider.Register([new SignCommand()]);

        provider.Current.TryFind("sign", out _).Should().BeTrue();
        _log.AtLeast(LogLevel.Warning).Should().ContainSingle(x => x.Message.Contains("'sign'"));
    }

    [Fact]
    public void ACommandThatNamesNoCategory_IsListedUnderGeneral()
    {
        var provider = NewProvider();
        provider.Register([new BootCommand(), new ProbeCommand()]);

        provider.Current.TryFind("boot", out var general);
        provider.Current.TryFind("probe", out var roleplay);

        general.Category.Should().Be(CommandCategories.GENERAL);
        roleplay.Category.Should().Be("Roleplay");
    }

    [Fact]
    public void TryFind_MatchesNameAndAlias_IgnoringCase()
    {
        var provider = NewProvider();
        provider.Register([new BootCommand()]);

        provider.Current.TryFind("BOOT", out var byName).Should().BeTrue();
        provider.Current.TryFind("Eject", out var byAlias).Should().BeTrue();

        byAlias.Should().BeSameAs(byName);
    }

    [Fact]
    public void Dispose_TakesTheCommandsOutAgain_AndRaisesChanged()
    {
        var provider = NewProvider();
        var changes = 0;
        provider.Changed += () => changes++;

        var registration = provider.Register([new BootCommand()]);
        registration.Dispose();
        registration.Dispose();

        provider.Current.TryFind("boot", out _).Should().BeFalse();
        changes.Should().Be(2);
    }

    [Fact]
    public void HotelAlias_IsAddedBesideTheDeclaredNames()
    {
        var provider = NewProvider();
        provider.Register([new BootCommand()]);

        provider.SetHotelAliases(
            new Dictionary<string, IReadOnlyList<string>> { ["boot"] = ["expulsar"] }
        );

        provider.Current.TryFind("expulsar", out var found).Should().BeTrue();
        found.Name.Should().Be("boot");
        provider.Current.TryFind("boot", out _).Should().BeTrue();
        provider.Current.TryFind("eject", out _).Should().BeTrue();
    }

    [Fact]
    public void HotelAlias_ThatClashes_IsIgnoredAndTheDeclaredNameKeepsWorking()
    {
        var provider = NewProvider();
        provider.Register([new BootCommand(), new HelloCommand()]);

        provider.SetHotelAliases(
            new Dictionary<string, IReadOnlyList<string>> { ["hello"] = ["boot"] }
        );

        provider.Current.TryFind("boot", out var found).Should().BeTrue();
        found.Name.Should().Be("boot");
        _log.AtLeast(LogLevel.Warning).Should().Contain(x => x.Message.Contains("'boot'"));
    }

    [Fact]
    public void TryFind_AllocatesNothing_ForAHitOrAMiss()
    {
        var provider = NewProvider();
        provider.Register([new BootCommand()]);
        var registry = provider.Current;
        var line = "unknownword".AsSpan();

        // Warm up so JIT and one-time caches are out of the measurement.
        registry.TryFind(line, out _);
        registry.TryFind("boot", out _);

        var before = GC.GetAllocatedBytesForCurrentThread();

        for (var i = 0; i < 1000; i++)
        {
            registry.TryFind(line, out _);
            registry.TryFind("BOOT", out _);
        }

        GC.GetAllocatedBytesForCurrentThread().Should().Be(before);
    }
}
