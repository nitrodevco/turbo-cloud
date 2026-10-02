using FluentAssertions;
using Turbo.Commands;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Commands.Enums;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Snapshots.Permissions;
using Turbo.Primitives.Texts;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Commands;

[CommandBranch("add", typeof(GrammarAddArguments), Permission = "test.add")]
[CommandBranch("remove", typeof(GrammarRemoveArguments))]
public abstract record GrammarArguments;

public sealed record GrammarAddArguments(
    [CommandParameter(Description = "Display name", MinLength = 2, MaxLength = 20)] string Name,
    [CommandParameter(Minimum = "1", Maximum = "10")] int Amount = 1
) : GrammarArguments;

public sealed record GrammarRemoveArguments(string Name) : GrammarArguments;

[Command("grammar")]
[RequiresPermission("test.grammar")]
public sealed class GrammarCommand : IOperatorCommand<GrammarArguments>
{
    public ValueTask<CommandResult> ExecuteAsync(
        IOperatorCommandContext ctx,
        GrammarArguments args,
        CancellationToken ct
    ) => ValueTask.FromResult(CommandResult.Ok);
}

public readonly record struct ArgumentCode(string Value);

public sealed class ArgumentCodeParser : ICommandArgumentParser
{
    public Type ValueType => typeof(ArgumentCode);
    public CommandParameterKind Kind => CommandParameterKind.Word;

    public bool TryParse(string text, out object? value)
    {
        value = new ArgumentCode(text);
        return text.StartsWith("code_", StringComparison.Ordinal);
    }
}

public sealed record CodeArguments([CommandParameter(Sensitive = true)] ArgumentCode Code);

[Command("code")]
[RequiresPermission("test.code")]
public sealed class CodeCommand : IOperatorCommand<CodeArguments>
{
    public ValueTask<CommandResult> ExecuteAsync(
        IOperatorCommandContext ctx,
        CodeArguments args,
        CancellationToken ct
    ) => ValueTask.FromResult(CommandResult.Ok);
}

public class CommandGrammarTests
{
    public sealed record InvalidDefaultArguments(
        [CommandParameter(Minimum = "1", Maximum = "10")] int Count = 100
    );

    [Command("invaliddefault")]
    [RequiresPermission("test.invaliddefault")]
    private sealed class InvalidDefaultCommand : IOperatorCommand<InvalidDefaultArguments>
    {
        public ValueTask<CommandResult> ExecuteAsync(
            IOperatorCommandContext ctx,
            InvalidDefaultArguments args,
            CancellationToken ct
        ) => ValueTask.FromResult(CommandResult.Ok);
    }

    private readonly CommandRegistryProvider _registry = new(
        new CapturingLogger<ICommandRegistryProvider>()
    );

    public CommandGrammarTests() => _registry.Register([new GrammarCommand()]);

    private ICommandBinder Binder
    {
        get
        {
            _registry.Current.TryFind("grammar", out var command);
            return command.Binder;
        }
    }

    [Fact]
    public void BranchesHaveIndependentArgumentsAndRejectIgnoredExtras()
    {
        Binder.Bind("add Alice 3", null).Arguments.Should().Be(new GrammarAddArguments("Alice", 3));
        Binder
            .Bind("remove Alice", null)
            .Arguments.Should()
            .Be(new GrammarRemoveArguments("Alice"));
        Binder.Bind("remove Alice 3", null).Succeeded.Should().BeFalse();
        Binder.Bind("add Alice", null).Arguments.Should().Be(new GrammarAddArguments("Alice"));
        Binder.Bind("add Alice", null).RequiredPermission.Should().Be("test.add");
    }

    [Fact]
    public void InvalidDefaultsAreRejectedAtRegistration()
    {
        Action register = () => _registry.Register([new InvalidDefaultCommand()]);
        register.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void UnmatchedInputDoesNotRevealRestrictedBranches()
    {
        Binder.Bind("", null, _ => false).Parameters.Should().Equal(":grammar remove <name>");
        Binder
            .Bind("unknown", null, _ => false)
            .Parameters.Should()
            .Equal(":grammar remove <name>");
        Binder
            .Bind("add Alice", null, _ => false)
            .ErrorKey.Should()
            .Be(CommandReplyKeys.NO_PERMISSION);
    }

    [Fact]
    public void QuotedArgumentsAreDecodedWithoutLosingTheirSourceRange()
    {
        Binder
            .Bind("add \"Alice Smith\" 3", null)
            .Arguments.Should()
            .Be(new GrammarAddArguments("Alice Smith", 3));
        Binder
            .Bind("add \"A\\\"B\" 3", null)
            .Arguments.Should()
            .Be(new GrammarAddArguments("A\"B", 3));
        var invalid = Binder.Bind("add Alice 11", null);
        invalid.ErrorKey.Should().Be(CommandReplyKeys.CONSTRAINT);
        invalid.ErrorStart.Should().Be(10);
        invalid.ErrorEnd.Should().Be(12);
    }

    [Theory]
    [InlineData("add \"unterminated")]
    [InlineData("add \"bad\\escape\"")]
    [InlineData("add \"Alice\"extra")]
    [InlineData("add A")]
    [InlineData("add Alice 0")]
    public void InvalidSyntaxOrConstraintsAreRejected(string input) =>
        Binder.Bind(input, null).Succeeded.Should().BeFalse();

    [Fact]
    public void HiddenBranchesAreAbsentFromTreeAndPermissionKeyChanges()
    {
        var permissions = ResolvedPermissionsSnapshot.EMPTY with { Granted = ["test.grammar"] };
        var texts = new Fakes().Create<IHotelTextProvider>();
        var tree = CommandTreeBuilder.Build(_registry.Current, permissions, texts);
        tree.Commands.Single().Syntax.Select(x => x.Path).Should().Equal("remove");
        tree.Commands.Single().Usage.Should().NotContain("add");
        CommandTreeBuilder
            .Key(_registry.Current, permissions)
            .Should()
            .NotBe(
                CommandTreeBuilder.Key(
                    _registry.Current,
                    permissions with
                    {
                        Granted = ["test.grammar", "test.add"],
                    }
                )
            );
    }

    [Fact]
    public void PluginArgumentParsersAreRegisteredAndSensitiveInputIsRedacted()
    {
        var parsers = new CommandArgumentParserRegistry();
        using var registration = parsers.Register([new ArgumentCodeParser()]);
        var registry = new CommandRegistryProvider(
            new CapturingLogger<ICommandRegistryProvider>(),
            parsers
        );
        registry.Register([new CodeCommand()]);
        registry.Current.TryFind("code", out var command);
        command
            .Binder.Bind("code_example", null)
            .Arguments.Should()
            .Be(new CodeArguments(new ArgumentCode("code_example")));
        command.Binder.Bind("secret", null).Parameters.Should().NotContain("secret");
        command.Binder.AuditText("malformed secret").Should().Be("[redacted]");
        registration.Dispose();
        parsers.Find(typeof(ArgumentCode)).Should().BeNull();
    }
}
