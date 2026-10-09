using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Main.Settings;
using Turbo.Primitives.Settings;

namespace Turbo.Tests.Support;

/// <summary>
/// The server's settings as the host builds them, over a test database: an
/// <c>appsettings.json</c> of the test's own, the panel's overrides read from
/// <c>server_settings</c> above it, and environment variables (under <see cref="EnvironmentPrefix"/>)
/// above those. The test's config classes are registered as modules register theirs.
/// <see cref="Restart"/> builds it all again over the same file and database, as a restart does.
/// </summary>
internal sealed class SettingsHarness : IDisposable
{
    private readonly IDbContextFactory<Turbo.Database.Context.TurboDbContext> _db;
    private readonly string _folder;
    private readonly Action<IServiceCollection, IConfiguration> _register;
    private readonly ServiceProvider _services;

    public SettingsHarness(
        IDbContextFactory<Turbo.Database.Context.TurboDbContext> db,
        string appSettings,
        Action<IServiceCollection, IConfiguration> register
    )
        : this(db, NewFolder(appSettings), register, $"TURBOTEST{Guid.NewGuid():N}__") { }

    private SettingsHarness(
        IDbContextFactory<Turbo.Database.Context.TurboDbContext> db,
        string folder,
        Action<IServiceCollection, IConfiguration> register,
        string environmentPrefix
    )
    {
        _db = db;
        _folder = folder;
        _register = register;
        EnvironmentPrefix = environmentPrefix;

        var configuration = new ConfigurationManager();

        configuration.AddJsonFile(
            Path.Combine(folder, "appsettings.json"),
            optional: false,
            reloadOnChange: false
        );
        configuration.AddEnvironmentVariables(environmentPrefix);
        // Where the host puts it: after the last file, so under the environment.
        ServerSettingsConfiguration.AddTo(configuration, Load);

        Configuration = configuration;

        var services = new ServiceCollection();

        register(services, configuration);

        var registry = ServerSettingRegistry.From(services);

        _services = services.BuildServiceProvider();
        Settings = new ServerSettingsService(
            configuration,
            _services,
            db,
            registry,
            Options.Create(new ServerSettingsConfig()),
            NullLogger<ServerSettingsService>.Instance
        );
    }

    /// <summary>What an environment variable's name starts with to reach this configuration.</summary>
    public string EnvironmentPrefix { get; }

    public IConfiguration Configuration { get; }

    public ServerSettingsService Settings { get; }

    /// <summary>The options of the class as the server took them: what it runs with until a restart.</summary>
    public T Running<T>()
        where T : class => _services.GetRequiredService<IOptions<T>>().Value;

    /// <summary>The same settings built again from the same file and database, as after a restart.</summary>
    public SettingsHarness Restart() => new(_db, _folder, _register, EnvironmentPrefix);

    public void Dispose()
    {
        Settings.Dispose();
        _services.Dispose();
    }

    private ServerSettingsLoad Load()
    {
        using var dbCtx = _db.CreateDbContext();

        return new ServerSettingsLoad(
            [
                .. dbCtx
                    .ServerSettings.AsNoTracking()
                    .OrderBy(x => x.Id)
                    .Select(x => new { x.Path, x.Value })
                    .AsEnumerable()
                    .Select(x => (x.Path, x.Value)),
            ],
            null
        );
    }

    private static string NewFolder(string appSettings)
    {
        var folder = Path.Combine(
            Path.GetTempPath(),
            "turbo-settings-" + Guid.NewGuid().ToString("N")
        );

        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "appsettings.json"), appSettings);

        return folder;
    }
}

/// <summary>A config section of every kind of setting, for tests of the settings themselves.</summary>
public sealed class TestHotelConfig
{
    public const string SECTION_NAME = "Test:Hotel";

    /// <summary>What the hotel is called, as players see it.</summary>
    public string Name { get; init; } = "Turbo";

    public int MaxUsers { get; init; } = 25;

    public double Ratio { get; init; } = 0.5;

    public bool Open { get; init; } = true;

    public TestHotelMode Mode { get; init; } = TestHotelMode.Normal;

    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(5);

    public string[] Layouts { get; init; } = ["default"];

    public Dictionary<string, int> Prices { get; init; } = [];

    [SecretSetting]
    public string ApiKey { get; init; } = "";

    [StartupSetting]
    public int Port { get; init; } = 3000;

    public TestHotelLinks Links { get; init; } = new();

    /// <summary>Worked out, not a setting.</summary>
    public bool IsNamed => Name.Length > 0;
}

/// <summary>A nested section of <see cref="TestHotelConfig"/>.</summary>
public sealed class TestHotelLinks
{
    public string Site { get; init; } = "https://hotel.example";
}

public enum TestHotelMode
{
    Normal,
    Maintenance,
}
