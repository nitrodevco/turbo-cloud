using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Core.Authorization;

public delegate Task<bool> PolicyRunner<TContext>(
    TContext ctx,
    List<Failure> failures,
    CancellationToken ct
);
