using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace Turbo.Tests.Architecture;

/// <summary>
/// The core is a framework others build on: what a private hotel or plugin is called stays in
/// that project, never in the core's code, docs or settings. The names are kept here as SHA-256
/// hashes of the lower-case word, so this file does not name them either. Every word of every
/// text file is checked, camel-case parts on their own (a <c>NameTheme</c> class finds "name").
/// </summary>
public sealed partial class NoPrivateProjectNamesTests
{
    private static readonly HashSet<string> PRIVATE_NAMES =
    [
        "7640da40298286a6e462d5a80f1c608b0df8660fb13bd5aad0f32b6db68b42c0",
        "f6bb95882f20b1b445cf6ec466cfc07c89a8758ed3458fc9aced94cd9585552a",
        "f230fecccfc6eae2c3d3fea119b8bb157fd7e237e27ea7b43d04430e34ff6e46",
        "4a38989ac1848855bfe02dfeb9bd31b96fedeab9daf9c334fb9c5d443b195afc",
        "c7a3c181f0876f8cb92fb9f9be8d37b487e3ee7312fa98e5988f185ecc8dc295",
    ];

    private static readonly HashSet<string> SKIPPED_DIRECTORIES = new(
        ["bin", "obj", ".git", ".vs", ".idea", "node_modules", "turbo-admin", "turbo-web"],
        StringComparer.OrdinalIgnoreCase
    );

    private static readonly HashSet<string> TEXT_EXTENSIONS = new(
        [
            ".cs",
            ".csproj",
            ".props",
            ".targets",
            ".sln",
            ".md",
            ".json",
            ".txt",
            ".yml",
            ".yaml",
            ".xml",
            ".sql",
            ".sh",
            ".ps1",
            ".py",
            ".js",
            ".mjs",
            ".ts",
            ".tsx",
            ".conf",
            ".editorconfig",
        ],
        StringComparer.OrdinalIgnoreCase
    );

    [Fact]
    public void No_core_file_names_a_private_project()
    {
        var root = RepositoryRoot();
        var seen = new ConcurrentDictionary<string, bool>(StringComparer.Ordinal);

        var offenders = Files(new DirectoryInfo(root))
            .AsParallel()
            .Where(file => NamesOne(File.ReadAllText(file.FullName), seen))
            .Select(file => Path.GetRelativePath(root, file.FullName));

        offenders.Should().BeEmpty("a private project's name is in these core files");
    }

    [Fact]
    public void The_scan_reads_the_core_sources() =>
        Files(new DirectoryInfo(RepositoryRoot())).Count().Should().BeGreaterThan(5000);

    private static bool NamesOne(string text, ConcurrentDictionary<string, bool> seen)
    {
        foreach (var match in Word().EnumerateMatches(text))
        {
            // Only words as long as a name are hashed.
            if (match.Length is < 4 or > 7)
                continue;

            var word = text.Substring(match.Index, match.Length).ToLowerInvariant();
            var hit = seen.GetOrAdd(
                word,
                w =>
                    PRIVATE_NAMES.Contains(
                        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(w)))
                    )
            );

            if (hit)
                return true;
        }

        return false;
    }

    private static IEnumerable<FileInfo> Files(DirectoryInfo dir) =>
        dir.EnumerateFiles()
            .Where(f => TEXT_EXTENSIONS.Contains(f.Extension))
            .Concat(
                dir.EnumerateDirectories()
                    .Where(d => !SKIPPED_DIRECTORIES.Contains(d.Name))
                    .SelectMany(Files)
            );

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

    // A word, or one part of a camel-case one: HiddenTheme -> hidden, theme; HTTPServer -> http, server.
    [GeneratedRegex(@"[A-Z]?[a-z]+|[A-Z]+(?![a-z])")]
    private static partial Regex Word();
}
