namespace Turbo.Primitives.Catalog.Enums;

/// <summary>Where a claim on an LTD series stands: waiting for its draw, won (with a serial), or lost.</summary>
public enum LtdEntryResult : byte
{
    Pending = 0,
    Won = 1,
    Lost = 2,
}
