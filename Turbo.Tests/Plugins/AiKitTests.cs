using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Turbo.Primitives.Plugins;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Plugins;

/// <summary>
/// <c>ai/</c> is what a plugin's <c>npm run ai</c> installs for its AI assistant
/// (docs/turbo-boost.md): code an assistant copies from a skill must build against this server,
/// so every <c>csharp</c> block of a skill is compiled and loaded here as the plugin the skill
/// names, its <c>csharp test</c> blocks compiled against it and the test kit, and every guide
/// the kit points to must be in <c>docs/</c>. A change to the plugin surface that breaks a skill
/// fails here, not in an assistant's first build.
/// </summary>
public sealed partial class AiKitTests : IDisposable
{
    private static readonly string Kit = Path.Combine(RepositoryRoot(), "ai");

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(),
        "turbo-ai-kit-" + Guid.NewGuid().ToString("N")
    );

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _db.Dispose();

        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }

    public static TheoryData<string> Skills() =>
        [
            .. Directory
                .EnumerateDirectories(Path.Combine(Kit, "skills"))
                .Select(directory => Path.GetFileName(directory))
                .Order(),
        ];

    [Theory]
    [MemberData(nameof(Skills))]
    public async Task A_skills_code_compiles_and_loads_as_the_plugin_it_names(string skill)
    {
        var text = await File.ReadAllTextAsync(Path.Combine(Kit, "skills", skill, "SKILL.md"), Ct);
        var code = Blocks(text, "csharp");
        var tests = Blocks(text, "csharp test");

        if (code.Count == 0)
        {
            tests.Should().BeEmpty("a test block tests the skill's code");

            return;
        }

        var key = CompiledAs().Match(text) is { Success: true } named
            ? named.Groups[1].Value
            : "demo";

        PluginBuilder.Write(_folder, key, code);

        var manager = new PluginTestHost(_db, _fakes).Manager(_folder);

        await manager.LoadAllAsync(ct: Ct);

        var status = manager.Snapshot().Single();
        status.State.Should().Be(PluginStateType.Loaded, $"{skill}: {status.Error}");

        if (tests.Count > 0)
            CompileWithTestKit([.. code, .. tests]).Should().BeEmpty(skill);
    }

    [Fact]
    public void Every_skill_names_itself_and_says_what_it_is_for()
    {
        foreach (var folder in Directory.EnumerateDirectories(Path.Combine(Kit, "skills")))
        {
            var text = File.ReadAllText(Path.Combine(folder, "SKILL.md")).ReplaceLineEndings("\n");
            var front = FrontMatter().Match(text);

            front.Success.Should().BeTrue($"{folder} starts with a --- front matter block");
            Field(front.Value, "name").Should().Be(Path.GetFileName(folder));
            Field(front.Value, "name").Should().MatchRegex("^[a-z0-9]+(-[a-z0-9]+)*$");
            Field(front.Value, "name")!.Length.Should().BeLessThanOrEqualTo(64);
            Field(front.Value, "description")
                .Should()
                .NotBeNullOrWhiteSpace()
                .And.Match(x => x!.Length <= 1024);
        }
    }

    [Fact]
    public void Every_guide_the_kit_points_to_is_in_docs()
    {
        var docs = Path.Combine(RepositoryRoot(), "docs");
        var missing = Directory
            .EnumerateFiles(Kit, "*.md", SearchOption.AllDirectories)
            .SelectMany(file =>
                File.ReadLines(file)
                    // The client's own guides are nitro-next's.
                    .Where(line => !line.Contains("nitro-next", StringComparison.Ordinal))
                    .SelectMany(line => GuideName().Matches(line).Select(m => m.Groups[1].Value))
                    .Where(name => !File.Exists(Path.Combine(docs, name)))
                    .Select(name => $"{Path.GetRelativePath(Kit, file)}: {name}")
            )
            .ToList();

        missing.Should().BeEmpty();
    }

    /// <summary>The blocks fenced as <paramref name="info"/> exactly, each a whole file.</summary>
    private static List<string> Blocks(string markdown, string info) =>
        [
            .. Fence()
                .Matches(markdown.ReplaceLineEndings("\n"))
                .Where(m => m.Groups[1].Value.Trim() == info)
                .Select(m => m.Groups[2].Value),
        ];

    private static string? Field(string front, string name) =>
        Regex.Match(front, $@"^{name}:\s*(.+)$", RegexOptions.Multiline) is { Success: true } m
            ? m.Groups[1].Value.Trim()
            : null;

    /// <summary>The plugin's code and its tests, with every assembly the test run has: xunit, the test kit, the server.</summary>
    private static IEnumerable<string> CompileWithTestKit(IReadOnlyList<string> sources)
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(file => MetadataReference.CreateFromFile(file));
        var compilation = CSharpCompilation.Create(
            "aikit",
            sources.Select(source =>
                CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.CSharp14))
            ),
            references,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable
            )
        );

        return compilation
            .GetDiagnostics(Ct)
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => d.ToString());
    }

    private static string RepositoryRoot()
    {
        for (
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            dir is not null;
            dir = dir.Parent
        )
            if (File.Exists(Path.Combine(dir.FullName, "Turbo.Cloud.sln")))
                return dir.FullName;

        throw new InvalidOperationException("Turbo.Cloud.sln was not found above the tests.");
    }

    [GeneratedRegex(@"^```([^\n`]*)\n(.*?)^```", RegexOptions.Multiline | RegexOptions.Singleline)]
    private static partial Regex Fence();

    [GeneratedRegex(@"\A---\n.*?\n---\n", RegexOptions.Singleline)]
    private static partial Regex FrontMatter();

    [GeneratedRegex(@"<!-- compiled as the plugin ""([a-z0-9_.-]+)"" -->")]
    private static partial Regex CompiledAs();

    [GeneratedRegex(@"`([a-z0-9-]+\.md)`")]
    private static partial Regex GuideName();
}
