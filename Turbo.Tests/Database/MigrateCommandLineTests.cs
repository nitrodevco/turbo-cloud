using FluentAssertions;
using Turbo.Database.Migrations;
using Xunit;

namespace Turbo.Tests.Database;

/// <summary><c>Turbo.Main migrate</c>: what is a command, what is an option, what is left for the host.</summary>
public sealed class MigrateCommandLineTests
{
    [Theory]
    [InlineData("migrate", true)]
    [InlineData("MIGRATE", true)]
    [InlineData("migrate --status", true)]
    [InlineData("--migrate", false)]
    [InlineData("migrations", false)]
    [InlineData("", false)]
    public void OnlyTheFirstWordIsTheCommand(string line, bool expected) =>
        MigrateCommandLine
            .IsMigrateCommand(line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Should()
            .Be(expected);

    [Fact]
    public void WithNoOptionsItAppliesWhatIsPending()
    {
        var options = MigrateCommandLine.Parse(["migrate"]);

        options.Status.Should().BeFalse();
        options.ScriptPath.Should().BeNull();
        options.AllowDestructive.Should().BeFalse();
        options.Help.Should().BeFalse();
        options.Error.Should().BeNull();
        options.HostArgs.Should().BeEmpty();
    }

    [Fact]
    public void TheOptionsAreReadInAnyOrderAndCase()
    {
        var options = MigrateCommandLine.Parse(["migrate", "--ALLOW-DESTRUCTIVE", "--status"]);

        options.Status.Should().BeTrue();
        options.AllowDestructive.Should().BeTrue();
        options.Error.Should().BeNull();
    }

    [Fact]
    public void AScriptNeedsAFile()
    {
        MigrateCommandLine
            .Parse(["migrate", "--script", "turbo.sql"])
            .ScriptPath.Should()
            .Be("turbo.sql");

        MigrateCommandLine.Parse(["migrate", "--script"]).Error.Should().Contain("needs a file");
        MigrateCommandLine
            .Parse(["migrate", "--script", "--status"])
            .Error.Should()
            .Contain("needs a file");
    }

    [Fact]
    public void StatusAndScriptAreDifferentThings()
    {
        MigrateCommandLine
            .Parse(["migrate", "--status", "--script", "x.sql"])
            .Error.Should()
            .Contain("cannot be used together");
    }

    [Theory]
    [InlineData("-h")]
    [InlineData("--help")]
    public void HelpIsAskedForEitherWay(string flag) =>
        MigrateCommandLine.Parse(["migrate", flag]).Help.Should().BeTrue();

    [Fact]
    public void WhateverItDoesNotKnowIsLeftForTheHostsConfiguration()
    {
        var options = MigrateCommandLine.Parse([
            "migrate",
            "--Turbo:Database:ConnectionString=server=db",
            "--status",
            "Orleans=1",
        ]);

        options.HostArgs.Should().Equal("--Turbo:Database:ConnectionString=server=db", "Orleans=1");
        options.Status.Should().BeTrue();
    }

    [Fact]
    public void TheUsageNamesEveryOption() =>
        MigrateCommandLine
            .USAGE.Should()
            .Contain("--status")
            .And.Contain("--script <file>")
            .And.Contain("--allow-destructive");

    [Fact]
    public void TheMigrationRunOptionsDefaultToTheConfiguration()
    {
        var run = new MigrationRunOptions();

        run.Mode.Should().BeNull();
        run.AllowDestructive.Should().BeNull();
        run.QuietWhenCurrent.Should().BeFalse();
    }

    [Fact]
    public void AStateIsOutOfOrderOnlyWhenSomethingPendingIsOlderThanSomethingApplied()
    {
        new MigrationState(["b", "c"], ["a"], [], true).HasOutOfOrderPending.Should().BeTrue();
        new MigrationState(["a", "b"], ["c"], [], true).HasOutOfOrderPending.Should().BeFalse();
        new MigrationState([], ["a", "b"], [], false).HasOutOfOrderPending.Should().BeFalse();
        new MigrationState(["a"], [], [], true).HasOutOfOrderPending.Should().BeFalse();
    }

    [Fact]
    public void AMigrationProblemIsFoundWhereverTheHostWrappedIt()
    {
        var problem = new MigrationException("Database X is behind.");

        MigrationException.FindIn(problem).Should().BeSameAs(problem);
        MigrationException
            .FindIn(new AggregateException(new InvalidOperationException(), problem))
            .Should()
            .BeSameAs(problem);
        MigrationException
            .FindIn(new InvalidOperationException("outer", new AggregateException(problem)))
            .Should()
            .BeSameAs(problem);
        MigrationException
            .FindIn(new AggregateException(new AggregateException(problem)))
            .Should()
            .BeSameAs(problem);
    }

    [Fact]
    public void ADifferentFailureIsNotMistakenForAMigrationProblem()
    {
        MigrationException.FindIn(null).Should().BeNull();
        MigrationException.FindIn(new InvalidOperationException()).Should().BeNull();
        MigrationException
            .FindIn(new AggregateException(new InvalidOperationException(), new TimeoutException()))
            .Should()
            .BeNull();
    }
}
