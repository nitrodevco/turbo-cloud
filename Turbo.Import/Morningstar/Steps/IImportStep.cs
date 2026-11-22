using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Import.Morningstar.Steps;

/// <summary>One part of an import: a table or a few that belong together.</summary>
internal interface IImportStep
{
    /// <summary>What it copies, for the log.</summary>
    string Name { get; }

    /// <summary>Whether it runs for these options (the catalog and chat logs are asked for).</summary>
    bool Applies(ImportContext ctx) => true;

    Task RunAsync(ImportContext ctx, CancellationToken ct);
}
