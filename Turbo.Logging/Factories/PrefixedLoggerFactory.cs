using Microsoft.Extensions.Logging;

namespace Turbo.Logging.Factories;

public sealed class PrefixedLoggerFactory(ILoggerFactory hostFactory, string prefix)
    : IPrefixedLoggerFactory
{
    private readonly ILoggerFactory _hostFactory = hostFactory;
    private readonly string _prefix = prefix;

    public ILogger CreateLogger(string categoryName) =>
        _hostFactory.CreateLogger($"{_prefix}:{categoryName}");
}
