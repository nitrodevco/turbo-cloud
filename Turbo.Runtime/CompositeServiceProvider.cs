using System;
using Microsoft.Extensions.DependencyInjection;

namespace Turbo.Runtime;

public sealed class CompositeServiceProvider(IServiceProvider primary, IServiceProvider secondary)
    : IServiceProvider
{
    private readonly IServiceProvider _primary = primary;
    private readonly IServiceProvider _secondary = secondary;

    public object? GetService(Type serviceType) =>
        _primary.GetService(serviceType) ?? _secondary.GetService(serviceType);

    public object GetRequiredService(Type serviceType) =>
        _primary.GetService(serviceType) ?? _secondary.GetRequiredService(serviceType);
}
