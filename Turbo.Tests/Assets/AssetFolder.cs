using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Turbo.Database.Context;
using Turbo.Gamedata.Assets;
using Turbo.Gamedata.Configuration;

namespace Turbo.Tests.Assets;

/// <summary>
/// A bundle store over a folder of its own under the temp directory, as the server's would be under
/// its content root; the folder goes when the test does.
/// </summary>
internal sealed class AssetFolder : IDisposable
{
    public AssetFolder(IDbContextFactory<TurboDbContext> db, AssetBundleConfig? config = null)
    {
        ContentRoot = Directory.CreateTempSubdirectory("turbo-assets-").FullName;
        Config = config ?? new AssetBundleConfig();
        Store = new AssetBundleStore(db, Options.Create(Config), new Environment(ContentRoot));
    }

    public string ContentRoot { get; }

    public AssetBundleConfig Config { get; }

    public AssetBundleStore Store { get; }

    public void Dispose()
    {
        if (Directory.Exists(ContentRoot))
            Directory.Delete(ContentRoot, recursive: true);
    }

    private sealed class Environment(string root) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Test";
        public string ApplicationName { get; set; } = "Turbo.Tests";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
