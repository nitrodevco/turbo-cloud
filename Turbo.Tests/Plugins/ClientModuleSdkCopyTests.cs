using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Turbo.Tests.Plugins;

/// <summary>
/// The SDK the server bundles plugins' client modules with (<c>Turbo.Plugins/ClientBuild/sdk/</c>)
/// is a copy of nitro-next's <c>packages/client-module-sdk</c> runtime. Each copied file is pinned
/// in <c>sdk-pins.json</c> beside it, with the nitro-next commit the copy was taken from: an edit
/// to the copy that did not come from <c>node scripts/client-module-sdk.mjs --write</c> fails
/// here, and the same script, run against a nitro-next checkout, tells when nitro-next moved on.
/// </summary>
public sealed class ClientModuleSdkCopyTests
{
    [Fact]
    public void Every_copied_file_is_the_one_pinned_with_the_nitro_next_commit_it_came_from()
    {
        var folder = Path.Combine(RepositoryRoot(), "Turbo.Plugins", "ClientBuild");
        var sdk = Path.Combine(folder, "sdk");
        using var pins = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(folder, "sdk-pins.json"))
        );

        pins.RootElement.GetProperty("nitroNextCommit")
            .GetString()
            .Should()
            .MatchRegex("^[0-9a-f]{40}$");

        var pinned = pins
            .RootElement.GetProperty("files")
            .EnumerateObject()
            .ToDictionary(x => x.Name, x => x.Value.GetString());
        var copied = Directory
            .EnumerateFiles(sdk, "*", SearchOption.AllDirectories)
            .Where(x => Path.GetExtension(x) is ".ts" or ".js")
            .ToDictionary(x => Path.GetRelativePath(sdk, x).Replace('\\', '/'), Sha256);

        copied
            .Should()
            .BeEquivalentTo(
                pinned,
                "the copy changes only by node scripts/client-module-sdk.mjs <nitro-next> --write"
            );
    }

    /// <summary>The file's SHA-256 with its line ends as committed (LF), whatever the checkout made them.</summary>
    private static string? Sha256(string path) =>
        Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(File.ReadAllText(path).Replace("\r\n", "\n")))
        );

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
}
