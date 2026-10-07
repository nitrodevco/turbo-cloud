using Turbo.Primitives.WiredTrading.Snapshots;

namespace Turbo.Primitives.Rooms.Wired;

/// <summary>
/// A contract built while a stack fires, from a custom contract addon's settings and the
/// stack's selections, for the Initiate Transaction box beside it.
/// </summary>
public interface IWiredContractSource
{
    /// <summary>The contract as it stands for this firing; null when it is misconfigured.</summary>
    public WiredContractSnapshot? BuildContract(IWiredContext ctx);
}
