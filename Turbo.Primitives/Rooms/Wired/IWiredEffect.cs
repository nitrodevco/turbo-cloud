using System.Threading.Tasks;

namespace Turbo.Primitives.Rooms.Wired;

public interface IWiredEffect
{
    public Task ExecuteAsync(IWiredContext ctx);
}
