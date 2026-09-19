using System.Collections.Generic;
using Turbo.Primitives.Catalog.Snapshots;

namespace Turbo.Catalog.Grains;

internal sealed class CatalogLtdRaffleLiveState
{
    public required int SeriesId { get; init; }
    public LtdSeriesSnapshot? Series { get; set; }

    /// <summary>Entrants of the batch being collected, with their raffle weight.</summary>
    public Dictionary<int, double> CurrentBatchEntries { get; } = [];
    public string? CurrentBatchId { get; set; }
    public bool IsInBufferPeriod { get; set; }
    public bool RaffleFinished { get; set; }
}
