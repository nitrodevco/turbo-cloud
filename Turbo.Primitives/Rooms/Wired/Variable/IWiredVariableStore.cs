using System.Threading.Tasks;

namespace Turbo.Primitives.Rooms.Wired.Variable;

public interface IWiredVariableStore
{
    public bool TryGetValue(in WiredVariableKey key, out WiredVariableValue value);
    public Task<bool> GiveValueAsync(
        WiredVariableKey key,
        WiredVariableValue value,
        bool replace = false
    );
    public Task<bool> SetValueAsync(
        IWiredExecutionContext ctx,
        WiredVariableKey key,
        WiredVariableValue value
    );
    public bool RemoveValue(WiredVariableKey key);

    /// <summary>
    /// When the value on a target was created and last written (unix milliseconds). False when
    /// the store keeps no history for it.
    /// </summary>
    public bool TryGetTimestamps(
        in WiredVariableKey key,
        out long createdAtMs,
        out long updatedAtMs
    )
    {
        createdAtMs = 0;
        updatedAtMs = 0;

        return false;
    }
}
