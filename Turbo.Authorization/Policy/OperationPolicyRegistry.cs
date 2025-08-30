using System;
using System.Collections.Generic;
using Turbo.Core.Authorization;

namespace Turbo.Authorization.Policy;

public class OperationPolicyRegistry<TContext>
{
    private readonly Dictionary<Enum, IOperationPolicy<TContext>> _map = [];

    public OperationPolicyRegistry<TContext> Add(Enum op, IOperationPolicy<TContext> policy)
    {
        _map[op] = policy;
        return this;
    }

    public IOperationPolicy<TContext>? Get(Enum op) => _map.TryGetValue(op, out var p) ? p : null;
}
