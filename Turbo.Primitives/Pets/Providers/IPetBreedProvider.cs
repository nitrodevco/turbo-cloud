using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Pets.Snapshots;

namespace Turbo.Primitives.Pets.Providers;

/// <summary>The pet breeds table, loaded once: which palettes each pet type has and sells.</summary>
public interface IPetBreedProvider
{
    public ImmutableArray<PetBreedSnapshot> GetPalettes(int typeId);
    public PetBreedSnapshot? TryGetPalette(int typeId, int paletteId);
    public Task ReloadAsync(CancellationToken ct);
}
