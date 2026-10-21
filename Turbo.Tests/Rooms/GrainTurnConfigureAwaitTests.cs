using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Code that runs inside a grain's turn must never await with <c>ConfigureAwait(false)</c>: in
/// Orleans it leaves the grain's single-threaded scheduler, so whatever follows the await runs on
/// the thread pool alongside the grain's next turn. The comment above <c>[*Grain.cs]</c> in
/// <c>.editorconfig</c> explains why. The rule is for the code a grain runs in its turn (the grain,
/// its modules and systems, furni and wired logic, chat commands), not for the services it calls:
/// a service's own continuations never touch grain state, and the grain's await of it still
/// resumes on the grain's scheduler, so CA2007 rightly asks a service to configure its awaits.
/// ORLEANS0014 only checks <c>Grain</c> subclasses, so this reads the rest of that code's source.
/// <see cref="RoomRightsLoadTurnTests"/> shows what such an await broke.
/// </summary>
public sealed partial class GrainTurnConfigureAwaitTests
{
    // Besides any *Grain.cs, *Grain.*.cs and Grains/ folder: the furni and wired logic and the
    // chat commands, which run in the room's turn. Turbo.Commands is the command framework, not
    // the commands, and has no .editorconfig comment saying it runs in a turn.
    private static readonly string[] TurnHostedFolders =
    [
        "Turbo.Rooms/Object/",
        "Turbo.Rooms/Wired/",
        "Turbo.Rooms/Commands/",
        "Turbo.Operations/Commands/",
    ];

    [Fact]
    public void Code_run_in_a_grain_turn_never_awaits_with_ConfigureAwait_false()
    {
        var root = RepositoryRoot();
        var files = TurnHostedSources(root).ToList();
        files.Should().Contain(path => path.EndsWith("/Turbo.Rooms/Grains/RoomGrain.cs"));

        var offenders = new List<string>();
        foreach (var file in files)
        {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                if (ConfigureAwaitFalse().IsMatch(CodeOf(lines[i])))
                    offenders.Add($"{Path.GetRelativePath(root, file).Replace('\\', '/')}:{i + 1}");
            }
        }

        string.Join(Environment.NewLine, offenders)
            .Should()
            .BeEmpty(
                "code run in a grain's turn must await plainly (see the [*Grain.cs] comment in .editorconfig)"
            );
    }

    private static IEnumerable<string> TurnHostedSources(string root) =>
        Directory
            .EnumerateDirectories(root, "Turbo.*")
            .Where(project => Path.GetFileName(project) != "Turbo.Tests")
            .SelectMany(project =>
                Directory.EnumerateFiles(project, "*.cs", SearchOption.AllDirectories)
            )
            .Where(file =>
            {
                var path = Path.GetRelativePath(root, file).Replace('\\', '/');
                var folders = path.Split('/')[..^1];
                if (folders.Any(folder => folder is "obj" or "bin" or "Migrations"))
                    return false;

                return GrainFileName().IsMatch(Path.GetFileName(path))
                    || folders.Contains("Grains")
                    || TurnHostedFolders.Any(folder =>
                        path.StartsWith(folder, StringComparison.Ordinal)
                    );
            })
            .Select(file => file.Replace('\\', '/'));

    // The line without its // or /// comment; a // inside a string literal is code.
    private static string CodeOf(string line)
    {
        var inString = false;
        for (var i = 0; i < line.Length - 1; i++)
        {
            var c = line[i];
            if (inString)
            {
                if (c == '\\')
                    i++;
                else if (c == '"')
                    inString = false;
            }
            else if (c == '\'')
                i += line[i + 1] == '\\' ? 3 : 2;
            else if (c == '"')
                inString = true;
            else if (c == '/' && line[i + 1] == '/')
                return line[..i];
        }

        return line;
    }

    // The test binaries may be built anywhere (-p:OutDir), so fall back to this source file.
    private static string RepositoryRoot([CallerFilePath] string sourceFile = "")
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Path.GetDirectoryName(sourceFile) })
        {
            for (
                var dir = string.IsNullOrEmpty(start) ? null : new DirectoryInfo(start);
                dir is not null;
                dir = dir.Parent
            )
            {
                if (File.Exists(Path.Combine(dir.FullName, "Turbo.Cloud.sln")))
                    return dir.FullName;
            }
        }

        throw new InvalidOperationException("Turbo.Cloud.sln was not found above the tests.");
    }

    [GeneratedRegex(@"Grain(\..+)?\.cs$")]
    private static partial Regex GrainFileName();

    [GeneratedRegex(@"\.ConfigureAwait\s*\(\s*(continueOnCapturedContext\s*:\s*)?false\s*\)")]
    private static partial Regex ConfigureAwaitFalse();
}
