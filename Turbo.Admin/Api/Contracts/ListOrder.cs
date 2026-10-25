namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// The order a list was read in: the column it is sorted by (<see cref="Sort"/>, a key the list
/// names) and which way (<see cref="Dir"/>, <see cref="ASC"/> or <see cref="DESC"/>). Sent back
/// with every sortable list, so the panel marks the column the server took, the list's default
/// when the panel asked for none.
/// </summary>
public sealed record ListOrder(string Sort, string Dir)
{
    public const string ASC = "asc";
    public const string DESC = "desc";
}
