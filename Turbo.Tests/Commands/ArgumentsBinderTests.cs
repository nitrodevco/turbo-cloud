using FluentAssertions;
using Microsoft.Extensions.Logging;
using Turbo.Commands;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Commands;

public class ArgumentsBinderTests
{
    private readonly Fakes _fakes = new();
    private readonly ICommandBinder _binder;
    private readonly ICommandRoom _room;
    private readonly IRoomPlayer _alice;

    public ArgumentsBinderTests()
    {
        var provider = new CommandRegistryProvider(new CapturingLogger<ICommandRegistryProvider>());
        provider.Register([new BootCommand()]);
        provider.Current.TryFind("boot", out var descriptor);
        _binder = descriptor.Binder;

        _alice = _fakes.Create<IRoomPlayer>("alice");
        _room = _fakes.Create<ICommandRoom>();
        _fakes.Handlers["FindPlayer"] = call =>
            string.Equals(
                call.Args[0] as string,
                "alice",
                System.StringComparison.OrdinalIgnoreCase
            )
                ? _alice
                : null;
    }

    [Fact]
    public void Bind_NothingTyped_AnswersWithGeneratedUsage()
    {
        var result = _binder.Bind("", _room);

        result.ErrorKey.Should().Be(CommandReplyKeys.USAGE);
        result.Parameters.Should().Equal(":boot <target> <count> <size> [loud] [reason]");
    }

    [Fact]
    public void Bind_PlayerNotInTheRoom_NamesWhoWasNotFound()
    {
        var result = _binder.Bind("bob 3 small", _room);

        result.ErrorKey.Should().Be(CommandReplyKeys.TARGET_NOT_FOUND);
        result.Parameters.Should().Equal("bob");
    }

    [Theory]
    [InlineData("alice many small", "many", "count")]
    [InlineData("alice 3 huge", "huge", "size")]
    [InlineData("alice 3 7", "7", "size")] // a number that is no member of the enum
    [InlineData("alice 3 small maybe", "maybe", "loud")]
    public void Bind_ValueThatDoesNotFit_NamesTheValueAndTheParameter(
        string line,
        string value,
        string parameter
    )
    {
        var result = _binder.Bind(line, _room);

        result.ErrorKey.Should().Be(CommandReplyKeys.BAD_VALUE);
        result.Parameters[..2].Should().Equal(value, parameter);
    }

    [Fact]
    public void Bind_WordsAfterTheLastParameter_GoToTheRestOfLine()
    {
        var result = _binder.Bind("alice 3 small on spare words", _room);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Bind_MoreWordsThanAnArgumentlessCommandTakes_IsUsage()
    {
        var provider = new CommandRegistryProvider(new CapturingLogger<ICommandRegistryProvider>());
        provider.Register([new HelloCommand()]);
        provider.Current.TryFind("hello", out var hello);

        var result = hello.Binder.Bind("extra", _room);

        result.ErrorKey.Should().Be(CommandReplyKeys.USAGE);
        result.Parameters.Should().Equal(":hello");
        hello.Binder.Bind("", _room).Succeeded.Should().BeTrue();
    }

    private static ICommandBinder TagBinder()
    {
        var provider = new CommandRegistryProvider(new CapturingLogger<ICommandRegistryProvider>());
        provider.Register([new TagCommand()]);
        provider.Current.TryFind("tag", out var tag);

        return tag.Binder;
    }

    [Theory]
    [InlineData("throne true", null, true)] // the flag without the badge
    [InlineData("throne on", null, true)]
    [InlineData("throne false", null, false)]
    [InlineData("throne \"\" true", null, true)] // an empty word skips the badge
    [InlineData("throne - true", null, true)] // so does a dash
    [InlineData("throne -", null, false)]
    [InlineData("throne ACH_1 true", "ACH_1", true)]
    [InlineData("throne ACH_1", "ACH_1", false)]
    [InlineData("throne true true", "true", true)] // two words: the first is the badge
    [InlineData("throne \"true\" true", "true", true)] // quoted, it is the badge
    [InlineData("throne \"true\"", "true", false)]
    [InlineData("throne \"-\"", "-", false)]
    public void Bind_AnOptionalWordBeforeAFlag_CanBeSkipped(
        string line,
        string? badge,
        bool trusted
    )
    {
        var result = TagBinder().Bind(line, _room);

        result.Succeeded.Should().BeTrue(result.ErrorKey);
        var args = (TagArguments)result.Arguments!;
        args.Badge.Should().Be(badge);
        args.Trusted.Should().Be(trusted);
    }

    [Fact]
    public void Bind_AWordAFlagCannotTake_StillGoesToTheOptionalBeforeIt()
    {
        // No later boolean would take it: a required word keeps its own value.
        _binder.Bind("alice 3 small true", _room).Succeeded.Should().BeTrue();
        ((TestArguments)_binder.Bind("alice 3 small true", _room).Arguments!)
            .Loud.Should()
            .BeTrue();
    }

    [Fact]
    public void Bind_FillsOptionalsWithTheirDefaults()
    {
        var args = (TestArguments)_binder.Bind("ALICE 3 large", _room).Arguments!;

        args.Target.Should().BeSameAs(_alice);
        args.Count.Should().Be(3);
        args.Size.Should().Be(TestSize.Large);
        args.Loud.Should().BeFalse();
        args.Reason.Should().BeNull();
    }

    [Fact]
    public void Bind_RestOfLineKeepsTheRemainingWordsAndSpacing()
    {
        var args = (TestArguments)_binder.Bind("alice 3 small on  being   rude", _room).Arguments!;

        args.Loud.Should().BeTrue();
        args.Reason!.Value.Text.Should().Be("being   rude");
    }

    [Fact]
    public void Bind_ArgumentsAreUnfiltered()
    {
        var args = (TestArguments)
            _binder.Bind("alice 1 small off a very rude word", _room).Arguments!;

        args.Reason!.Value.Text.Should().Be("a very rude word");
    }

    [Fact]
    public void Bind_ARoomPlayer_WithNoRoomToLookInTo_SaysTheCommandNeedsOne()
    {
        var result = _binder.Bind("alice 3 small", null);

        result.ErrorKey.Should().Be(CommandReplyKeys.NEEDS_ROOM);
    }

    [Fact]
    public void Register_ARestOfLineThatIsNotLast_IsRefused()
    {
        var provider = new CommandRegistryProvider(new CapturingLogger<ICommandRegistryProvider>());

        var act = () => provider.Register([new MisplacedRestCommand()]);

        act.Should().Throw<System.InvalidOperationException>().WithMessage("*must be the last*");
    }

    [Fact]
    public void Register_AnUnsupportedParameterType_IsRefused()
    {
        var provider = new CommandRegistryProvider(new CapturingLogger<ICommandRegistryProvider>());

        var act = () => provider.Register([new UnsupportedParameterCommand()]);

        act.Should().Throw<System.InvalidOperationException>().WithMessage("*unsupported type*");
    }
}
