using System.Threading;
using System.Threading.Tasks;
using Orleans;

namespace Turbo.Primitives.Hotel.Grains;

/// <summary>
/// The message every player is shown when they log in, set by staff in the admin panel. Every
/// login asks, so it is answered from memory; an empty message means none is shown.
/// </summary>
public interface IWelcomeMessageGrain : IGrainWithStringKey
{
    /// <summary>The welcome message, or an empty string when there is none.</summary>
    public Task<string> GetMessageAsync(CancellationToken ct);

    /// <summary>
    /// Saves the welcome message, trimmed, for every login from now on. An empty message turns
    /// it off. Answers with what was saved.
    /// </summary>
    public Task<string> SetMessageAsync(string message, CancellationToken ct);
}
