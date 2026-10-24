namespace Turbo.Primitives.Rooms.Wired;

/// <summary>
/// A cap on how often a stack fires, as the execution limit add-on puts on it. The room asks the
/// budgets a firing's add-ons put in <see cref="IWiredPolicy.ExecutionBudgets"/> and spends one
/// execution from one of them when the firing goes ahead.
/// </summary>
public interface IWiredExecutionBudget
{
    /// <summary>Whether one more execution fits the budget now.</summary>
    public bool HasRoom();

    /// <summary>Counts one execution against the budget.</summary>
    public void Spend();
}
