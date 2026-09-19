namespace Turbo.Primitives.Rooms.Enums.Wired;

/// <summary>
/// Operations of the "change variable value" action. Codes are the ids the client offers in its
/// operation dropdown (wiredfurni.params.variables.operation.&lt;id&gt;). The basic block is 0..6, the
/// advanced block starts at 40. <see cref="Negate"/>, <see cref="Invert"/> and <see cref="Absolute"/>
/// take no operand.
/// </summary>
public enum WiredVariableOperationType
{
    Set = 0,
    Add = 1,
    Subtract = 2,
    Multiply = 3,
    Divide = 4,
    Modulo = 5,
    Power = 6,
    Minimum = 40,
    Maximum = 41,
    Random = 50,
    Negate = 60,
    BitwiseAnd = 100,
    BitwiseOr = 101,
    BitwiseXor = 102,
    Invert = 103,
    ShiftLeft = 104,
    ShiftRight = 105,
    Absolute = 110,
    IsEqual = 111,
    IsNotEqual = 112,
    IsLess = 113,
    IsGreater = 114,
    SetIfLess = 115,
    SetIfGreater = 116,
    AddClampedToOperand = 117,
    SubtractClampedToOperand = 118,
    Average = 119,
    Distance = 120,
    IsLessOrEqual = 121,
    IsGreaterOrEqual = 122,
}
