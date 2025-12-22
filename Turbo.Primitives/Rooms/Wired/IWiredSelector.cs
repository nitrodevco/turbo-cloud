namespace Turbo.Primitives.Rooms.Wired;

public interface IWiredSelector : IWiredDefinition
{
    public void Select(IWiredContext ctx);
}
