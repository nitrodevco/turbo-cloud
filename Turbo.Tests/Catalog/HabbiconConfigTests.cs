using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Turbo.Catalog;
using Turbo.Catalog.Configuration;
using Turbo.Main.Settings;
using Turbo.Plugins.Extensions;
using Xunit;

namespace Turbo.Tests.Catalog;

public class HabbiconConfigTests
{
    [Fact]
    public void The_shipped_settings_offer_habbos_collections_in_the_server_as_it_starts()
    {
        // As Program registers it: the module, then the settings, which bind lists whole. The
        // shipped file lists no collections ("Collections": []), so Habbo's are offered; the
        // reward tracks' Habbicon prizes are given from them.
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddJsonFile(
            Path.Combine(AppContext.BaseDirectory, "appsettings.json"),
            optional: false
        );
        builder.Services.AddHostPlugin<CatalogModule>(builder);
        builder.AddServerSettings();

        using var services = builder.Services.BuildServiceProvider();
        var config = services.GetRequiredService<IOptions<HabbiconConfig>>().Value;

        config
            .Collections.Select(x => x.Name)
            .Should()
            .Equal("duck", "duck2", "frank", "toast", "zombie");
    }
}
