using Turbo.Primitives.WiredTrading.Snapshots;

namespace Turbo.Primitives.Furniture.ExtraData;

/// <summary>
/// What a wired contract furni asks and gives, as its editor last saved it, under
/// <see cref="SECTION"/>. The contract's id and type are the furni's own, not this record's.
/// </summary>
public sealed class WiredContractData
{
    public const string SECTION = "wired_contract";

    public WiredContractSnapshot? Contract { get; set; }
}
