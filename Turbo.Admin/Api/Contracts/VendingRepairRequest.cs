using Turbo.Primitives.Gamedata.Snapshots;

namespace Turbo.Admin.Api.Contracts;

/// <summary>Explicit selections from the known-vending preview, with their expected versions.</summary>
public sealed record VendingRepairRequest(VendingRepairSelectionSnapshot[]? Items);
