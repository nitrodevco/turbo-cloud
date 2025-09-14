using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Turbo.Pipeline;

internal class OwnerCtx : IAsyncDisposable
{
    public IServiceProvider Root { get; }
    public IServiceScopeFactory ScopeFactory { get; }

    private readonly bool _createAmbient;
    private readonly object _gate = new();

    private AsyncServiceScope? _ambientScope;

    private OwnerCtx(IServiceProvider root, bool createAmbient)
    {
        Root = root ?? throw new ArgumentNullException(nameof(root));
        ScopeFactory = root.GetRequiredService<IServiceScopeFactory>();
        _createAmbient = createAmbient;
    }

    public static OwnerCtx Create(IServiceProvider root, bool createAmbient) =>
        new(root, createAmbient);

    public IServiceProvider AmbientProvider
    {
        get
        {
            // Reuse the root if no ambient scope is requested
            if (!_createAmbient)
                return Root;

            // Lazy, thread-safe creation of the ambient scope
            if (!_ambientScope.HasValue)
            {
                lock (_gate)
                {
                    if (!_ambientScope.HasValue)
                        _ambientScope = ScopeFactory.CreateAsyncScope();
                }
            }

            // Non-null: scope is created and stored for the lifetime of this OwnerCtx
            return _ambientScope.Value.ServiceProvider;
        }
    }

    public async ValueTask DisposeAsync()
    {
        // Dispose the ambient scope if we created one
        if (_ambientScope.HasValue)
        {
            await _ambientScope.Value.DisposeAsync().ConfigureAwait(false);
            _ambientScope = null;
        }
    }
}
