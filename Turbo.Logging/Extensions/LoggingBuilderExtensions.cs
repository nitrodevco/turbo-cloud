using Microsoft.Extensions.Logging;

namespace Turbo.Logging.Extensions;

public static class LoggingBuilderExtensions
{
    public static ILoggingBuilder AddTurboConsoleLogger(this ILoggingBuilder builder)
    {
        builder.ClearProviders();
        builder.AddConsoleFormatter<TurboConsoleFormatter, TurboConsoleFormatterOptions>();
        builder.AddConsole(opts =>
        {
            opts.FormatterName = TurboConsoleFormatter.FormatterName;
        });

        return builder;
    }
}
