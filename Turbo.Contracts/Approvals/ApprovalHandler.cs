using System;
using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Contracts.Approvals;

/// <summary>
/// Carries out approved requests of one kind (<c>docs/approvals.md</c>). A public class deriving
/// from <see cref="ApprovalHandler{TArgs}"/> in a plugin's assembly is found as the plugin loads,
/// built with its services and the host's, and taken away as it unloads; core registers its own as
/// <see cref="IApprovalHandler"/> services. One kind is one owner's: a second live handler of a
/// kind fails its plugin's load.
/// </summary>
public interface IApprovalHandler
{
    /// <summary>The kind it carries out (<see cref="ApprovalRequest.Kind"/>).</summary>
    string Kind { get; }

    /// <summary>The arguments' type, read from the saved JSON.</summary>
    Type ArgumentsType { get; }

    /// <summary>Carries out <paramref name="approval"/>, given its arguments as <see cref="ArgumentsType"/>.</summary>
    Task<ApprovalResult> ExecuteAsync(object arguments, Approval approval, CancellationToken ct);
}

/// <summary>
/// Carries out approved requests of <see cref="Kind"/>, taking <typeparamref name="TArgs"/> (the
/// request's <see cref="ApprovalRequest.Arguments"/>, read back from JSON). It runs once per
/// approved request, after the approver's yes, on the server the approver used; what it throws is
/// logged and the request is marked failed.
/// </summary>
public abstract class ApprovalHandler<TArgs> : IApprovalHandler
    where TArgs : notnull
{
    public abstract string Kind { get; }

    public Type ArgumentsType => typeof(TArgs);

    /// <summary>
    /// Does what was approved. <paramref name="approval"/> says who asked, who approved and their
    /// note. Answer <see cref="ApprovalResult.Done"/> or <see cref="ApprovalResult.Failed"/>.
    /// </summary>
    public abstract Task<ApprovalResult> ExecuteAsync(
        TArgs arguments,
        Approval approval,
        CancellationToken ct
    );

    Task<ApprovalResult> IApprovalHandler.ExecuteAsync(
        object arguments,
        Approval approval,
        CancellationToken ct
    ) => ExecuteAsync((TArgs)arguments, approval, ct);
}

/// <summary>What carrying out a request answered, kept with it and shown to the approver.</summary>
public sealed record ApprovalResult(bool Succeeded, string? Message)
{
    /// <summary>The longest message kept.</summary>
    public const int MESSAGE_MAX_LENGTH = 1000;

    public static ApprovalResult Done(string? message = null) => new(true, message);

    /// <summary>It could not be done: a sentence the approver can act on.</summary>
    public static ApprovalResult Failed(string message) => new(false, message);
}
