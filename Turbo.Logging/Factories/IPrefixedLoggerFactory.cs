using Microsoft.Extensions.Logging;

namespace Turbo.Logging.Factories;

public interface IPrefixedLoggerFactory
{
    ILogger CreateLogger(string categoryName);
}
