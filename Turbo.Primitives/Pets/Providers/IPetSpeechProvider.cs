using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Primitives.Pets.Providers;

/// <summary>What each pet type says, read from <c>pet_speech</c>.</summary>
public interface IPetSpeechProvider
{
    /// <summary>
    /// The lines a pet of this type may say: its own type's, or the lines every type shares when
    /// it has none. Empty when there are neither, and the pet stays quiet.
    /// </summary>
    public ImmutableArray<string> GetLines(int typeId);

    public Task ReloadAsync(CancellationToken ct);
}
