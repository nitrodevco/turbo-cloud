using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Ai;
using Turbo.Ai.Approvals;
using Turbo.Ai.Configuration;
using Turbo.Ai.Storage;
using Turbo.Primitives.Players;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Ai;

/// <summary>
/// The panel's AI page (<c>docs/ai-sdk.md</c>, The panel) over the real server settings: a
/// provider's key is written alone, the others kept (the panel never reads a key back, and the
/// keys are one setting replaced whole), and a blank one removes it.
/// </summary>
public sealed class AiPanelTests : IDisposable
{
    private const string APP_SETTINGS = """
        {
          "Turbo": {
            "Ai": {
              "Providers": {
                "claude": { "Driver": "anthropic", "Model": "claude-haiku-5-5" },
                "local": { "Driver": "openai-compatible", "Url": "http://localhost:11434/v1", "Model": "llama" }
              },
              "Keys": { "claude": "sk-ant-first-0123456789" }
            }
          }
        }
        """;

    private readonly SqliteDb _db = new();
    private readonly SettingsHarness _settings;
    private readonly AiHost _host;

    public AiPanelTests()
    {
        _settings = new SettingsHarness(
            _db,
            APP_SETTINGS,
            (services, config) =>
                services.Configure<AiConfig>(config.GetSection(AiConfig.SECTION_NAME))
        );

        var monitor = new LiveMonitor(() => _settings.Live<AiConfig>());
        var store = new MemoryAiStore();
        var runtime = new AiRuntime(
            monitor,
            new AiProviders(
                monitor,
                [],
                store,
                TimeProvider.System,
                NullLogger<AiProviders>.Instance
            ),
            store,
            new NoApprovals(),
            new Fakes().Create<IAiPermissions>(),
            new AiRateLimits(TimeProvider.System),
            TimeProvider.System,
            NullLogger<AiRuntime>.Instance
        );

        _host = new AiHost(runtime, NullLogger<AiHost>.Instance, _settings.Settings);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _host.Dispose();
        _settings.Dispose();
        _db.Dispose();
    }

    [Fact]
    public async Task A_providers_key_is_written_alone_the_others_kept_and_a_blank_one_removes_it()
    {
        await _host.SetKeyAsync("LOCAL", " sk-local-0123456789 ", new PlayerId(1), Ct);

        Keys()
            .Should()
            .Equal(
                new Dictionary<string, string>
                {
                    ["claude"] = "sk-ant-first-0123456789",
                    ["local"] = "sk-local-0123456789",
                }
            );
        _settings.Settings.GetValue("Turbo:Ai:Keys").Should().BeNull("a key is never read back");

        await _host.SetKeyAsync("claude", "", new PlayerId(1), Ct);

        Keys().Should().Equal(new Dictionary<string, string> { ["local"] = "sk-local-0123456789" });
        var refused = () => _host.SetKeyAsync("nobody", "sk-x-0123456789", new PlayerId(1), Ct);
        await refused.Should().ThrowAsync<ArgumentException>().WithMessage("*nobody*");
    }

    private Dictionary<string, string> Keys() =>
        _settings.Live<AiConfig>().Keys.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value);

    private sealed class LiveMonitor(Func<AiConfig> current) : IOptionsMonitor<AiConfig>
    {
        public AiConfig CurrentValue => current();

        public AiConfig Get(string? name) => current();

        public IDisposable? OnChange(Action<AiConfig, string?> listener) => null;
    }
}
