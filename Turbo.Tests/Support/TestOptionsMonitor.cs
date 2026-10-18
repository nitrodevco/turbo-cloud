using System;
using Microsoft.Extensions.Options;

namespace Turbo.Tests.Support;

/// <summary>
/// An <see cref="IOptionsMonitor{TOptions}"/> whose value a test sets, as a panel change does to
/// a setting read live: <see cref="Set"/> replaces what <see cref="CurrentValue"/> returns.
/// </summary>
public sealed class TestOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    where T : class
{
    public T CurrentValue { get; private set; } = value;

    public T Get(string? name) => CurrentValue;

    public void Set(T value) => CurrentValue = value;

    public IDisposable? OnChange(Action<T, string?> listener) => null;
}
