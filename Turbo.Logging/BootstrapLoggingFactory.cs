using Microsoft.Extensions.Logging;
using Turbo.Logging.Extensions;

namespace Turbo.Logging;

public static class BootstrapLoggingFactory
{
    public static ILoggerFactory CreateBootstrapLoggerFactory()
    {
        return LoggerFactory.Create(builder =>
        {
            builder.AddTurboConsoleLogger();
        });
    }
}
