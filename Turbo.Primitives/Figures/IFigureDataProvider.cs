using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Primitives.Figures;

/// <summary>
/// The hotel's figure data, as figures are checked against it (<see cref="FigureRules"/>). Read
/// from the gamedata when first asked for and kept a while; an edit or import here forgets it.
/// </summary>
public interface IFigureDataProvider
{
    public Task<FigureData> GetAsync(CancellationToken ct);

    /// <summary>Forgets the figure data read: the next ask reads it again.</summary>
    public void Invalidate();
}
