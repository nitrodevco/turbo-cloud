using Microsoft.Extensions.DependencyInjection;
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

        host.Logging.AddConfiguration(host.Configuration.GetSection("Logging"));
        host.Logging.AddTurboConsoleLogger();

        return host;
    }
}
