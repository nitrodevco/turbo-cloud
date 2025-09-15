using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Turbo.Logging.Extensions;

public static class HostApplicationBuilderExtensions
{
    public static HostApplicationBuilder AddTurboLogging(this HostApplicationBuilder host)
    {
        host.Services.Configure<TurboConsoleFormatterOptions>(
            host.Configuration.GetSection(TurboConsoleFormatterOptions.SECTION_NAME)
        );

        var factory = LoggerFactory.Create(builder =>
        {
            builder.ClearProviders();
            builder.AddConfiguration(host.Configuration.GetSection("Logging"));
            builder.AddTurboConsoleLogger();
        });

        host.Services.AddSingleton(factory);
        host.Services.TryAddSingleton(typeof(ILogger<>), typeof(Logger<>));

        return host;
    }
}
