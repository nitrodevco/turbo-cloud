namespace Turbo.Admin.Api.Contracts;

/// <summary>What the signed-in staff member may do to the whole hotel: one flag per command node.</summary>
public sealed record HotelAbilities(bool Alert, bool Maintenance, bool Shutdown);
