using System;

namespace Turbo.Primitives.Rooms.Wired;

public interface IWiredParamRule
{
    public int DefaultValue { get; }
    public Type? ValueType { get; }

    public bool IsValid(int value);
    public int Sanitize(int value);
    public object FromInt(int value);
    public int ToInt(object value);

    /// <summary>
    /// The hotel text (<c>wiredfurni.error.*</c>) a save gets when this rule refuses its value;
    /// null for the generic one.
    /// </summary>
    public string? ErrorKey => null;
}
