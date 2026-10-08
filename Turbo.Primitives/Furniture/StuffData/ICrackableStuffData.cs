namespace Turbo.Primitives.Furniture.StuffData;

/// <summary>
/// A crackable furni's progress (stuff data 7, Flash's <c>CrackableStuffData</c>): the hits it
/// has taken and the hits that crack it. Its legacy string is the state the furni draws.
/// </summary>
public interface ICrackableStuffData : IStuffData
{
    public int Hits { get; }
    public int Target { get; }

    /// <summary>Sets the progress and the state drawn for it.</summary>
    public void SetProgress(int hits, int target, int state);
}
