namespace Turbo.Primitives.Rooms.Wired;

public interface IWiredVariableRegistration
{
    public IWiredVariableDefinition Definition { get; }

    public bool TryGet(string key, out int value);
    public void SetValue(string key, int value);
    public void RemoveValue(string key);
}
