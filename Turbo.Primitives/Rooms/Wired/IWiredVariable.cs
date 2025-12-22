namespace Turbo.Primitives.Rooms.Wired;

public interface IWiredVariable : IWiredDefinition
{
    public void Apply(IWiredContext ctx);
}
