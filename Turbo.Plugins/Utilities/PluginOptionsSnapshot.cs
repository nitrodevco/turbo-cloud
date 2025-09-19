using System;
using Microsoft.Extensions.Options;

namespace Turbo.Plugins.Utilities;

internal sealed class PluginOptionsSnapshot<T>(IOptions<T> options) : IPluginOptionsSnapshot<T>
    where T : class
{
    public T Value { get; } = options?.Value ?? throw new ArgumentNullException(nameof(options));
}
