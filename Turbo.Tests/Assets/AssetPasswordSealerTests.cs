using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Gamedata.Assets.Publishing;
using Turbo.Gamedata.Configuration;
using Turbo.Primitives.Gamedata;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Assets;

/// <summary>
/// Publish targets' passwords sealed for the database: they come back as they went in with the same
/// key, a different key gives a refusal that says to enter the password again, and without a
/// configured key one is made once in the bundle folder and kept.
/// </summary>
public sealed class AssetPasswordSealerTests : IDisposable
{
    private readonly string _root = Directory
        .CreateTempSubdirectory("turbo-asset-sealer-")
        .FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void A_sealed_password_unseals_to_itself_and_is_not_kept_as_it_is()
    {
        var sealer = Sealer(Key());

        var sealedBytes = sealer.Seal("correct horse battery");

        Encoding.UTF8.GetString(sealedBytes).Should().NotContain("correct horse battery");
        sealer.Unseal(sealedBytes).Should().Be("correct horse battery");
        sealer
            .Seal("correct horse battery")
            .Should()
            .NotEqual(sealedBytes, "each seal has its own nonce");
    }

    [Fact]
    public void A_password_sealed_with_another_key_is_refused_with_a_reason_to_enter_it_again()
    {
        var sealedBytes = Sealer(Key()).Seal("secret");

        var unseal = () => Sealer(Key()).Unseal(sealedBytes);

        unseal
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage(AssetPasswordSealer.UNREADABLE);
    }

    [Fact]
    public void Without_a_configured_key_one_is_made_once_in_the_bundle_folder_and_kept()
    {
        var keyFile = Path.Combine(_root, AssetPasswordSealer.KEY_FILE);

        var sealedBytes = Sealer("").Seal("secret");

        File.Exists(keyFile).Should().BeTrue();
        var key = File.ReadAllText(keyFile);
        Convert.FromBase64String(key).Should().HaveCount(32);

        // A restart: a new sealer reads the same key rather than making another.
        Sealer("").Unseal(sealedBytes).Should().Be("secret");
        File.ReadAllText(keyFile).Should().Be(key);

        if (!OperatingSystem.IsWindows())
            File.GetUnixFileMode(keyFile)
                .Should()
                .Be(UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    private static string Key() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private AssetPasswordSealer Sealer(string secretKey)
    {
        var fakes = new Fakes();

        fakes.Handlers["get_Root"] = _ => _root;

        return new AssetPasswordSealer(
            Options.Create(new AssetBundleConfig { SecretKey = secretKey }),
            fakes.Create<IAssetBundleStore>(),
            NullLogger<AssetPasswordSealer>.Instance
        );
    }
}
