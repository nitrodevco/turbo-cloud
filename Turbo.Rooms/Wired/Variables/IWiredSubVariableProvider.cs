using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Wired.Variable;

namespace Turbo.Rooms.Wired.Variables;

/// <summary>
/// A box that contributes derived variables to the room (the level-up and time utility
/// addons). Rebuilt with the variable boxes whenever its tile changes.
/// </summary>
public interface IWiredSubVariableProvider
{
    public Task LoadWiredAsync(CancellationToken ct);
    public IEnumerable<IWiredVariable> GetSubVariables();
}
