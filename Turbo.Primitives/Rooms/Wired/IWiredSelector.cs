using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Enums.Wired;

namespace Turbo.Primitives.Rooms.Wired;

public interface IWiredSelector : IWiredBox
{
    public bool GetIsFilter();
    public bool GetIsInvert();

    /// <summary>Whether the selector picks furni; filtering and inverting work on these.</summary>
    public bool SelectsFurni { get; }

    /// <summary>Whether the selector picks users; filtering and inverting work on these.</summary>
    public bool SelectsUsers { get; }

    /// <summary>The order the users it picked are listed in.</summary>
    public WiredUserOrderType UserOrder { get; }

    public Task<IWiredSelectionSet> SelectAsync(IWiredProcessingContext ctx, CancellationToken ct);
}
