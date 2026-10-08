using Turbo.Primitives.Catalog.Snapshots;

namespace Turbo.Primitives.Catalog.Providers;

/// <summary>The hotel's gift wrapping, read from config against the loaded furniture definitions.</summary>
public interface IGiftWrappingProvider
{
    public GiftWrappingSnapshot GetWrapping();
}
