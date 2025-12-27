# Future Considerations

This document captures optimization options that were evaluated but deferred for AsyncMediator v3.0. These may be revisited in future versions based on user feedback and ecosystem changes.

## Evaluated and Deferred

### 1. ValueTask Instead of Task

**What**: Change all async return types from `Task<T>` to `ValueTask<T>`.

**Potential Benefit**: 5-15% memory reduction when operations complete synchronously (cache hits, in-memory lookups).

**Why Deferred**:
- Command/Query handlers almost always hit databases or external services (async by nature)
- `ValueTask<T>` has strict usage rules - must never be awaited twice, which is easy to violate in mediator patterns
- The complexity and misuse risk doesn't justify marginal gains
- If handlers complete synchronously, they should likely be refactored rather than optimized

**Reconsider When**: Benchmarks show >30% of operations completing synchronously.

**References**:
- [ValueTask Performance Masterclass](https://medium.com/@krativarshney7/async-await-is-killing-your-net-performance-the-value-task-masterclass-1db9d9a6a4fa)
- [ValueTask + IValueTaskSource Advanced Patterns](https://medium.com/@anderson.buenogod/valuetask-ivaluetasksource-in-net-performance-as-a-contract-advanced-217398cf2465)

---

### 2. AOT/Native Compilation Support

**What**: Make the library fully compatible with .NET Native AOT and trimming.

**Potential Benefit**: Faster startup, smaller deployments for serverless/edge scenarios.

**Why Deferred**:
- Requires replacing reflection with compiled expressions or source generators
- Compiled expressions add 3-4x code complexity
- Source generators add build complexity and debugging difficulty
- AOT scenarios (mobile, WASM, AWS Lambda) are niche for mediator patterns
- Current reflection is cached - only pays cost once per event type
- MediatR uses the same reflection pattern

**Current Workaround**: Pre-warm the cache at startup by sending a dummy command/event of each type.

**Reconsider When**: >30% of users request AOT support, or .NET ecosystem shifts heavily toward AOT.

**If Implemented**:
```csharp
// Option A: Compiled Expressions (partial AOT support)
private static Func<Mediator, object, Task> CreatePublishDelegate(Type eventType)
{
    var mediatorParam = Expression.Parameter(typeof(Mediator), "mediator");
    var eventParam = Expression.Parameter(typeof(object), "event");

    var method = typeof(Mediator)
        .GetMethod(nameof(PublishBoxed), BindingFlags.Instance | BindingFlags.NonPublic)!
        .MakeGenericMethod(eventType);

    var call = Expression.Call(mediatorParam, method, eventParam);
    return Expression.Lambda<Func<Mediator, object, Task>>(call, mediatorParam, eventParam).Compile();
}

// Option B: Source Generator (full AOT support) - separate package
[GenerateMediator]
public partial class GeneratedMediator { }
```

**References**:
- [Native AOT Migration Guide](https://medium.com/@dikhyantkrishnadalai/surviving-native-aot-the-reflection-migration-guide-every-net-architect-needs-fa3760fbb41b)
- [Creating AOT-Compatible Libraries](https://devblogs.microsoft.com/dotnet/creating-aot-compatible-libraries/)

---

### 3. Compiled Expressions for Factory

**What**: Replace delegate invocation with compiled expression trees for handler resolution.

**Potential Benefit**: ~50ns faster per resolution (from ~100ns to ~50ns).

**Why Deferred**:
- Factory resolution is not the bottleneck (total operation is ~200ns)
- Would add 50+ lines of complex expression tree code
- DI container overhead dominates the timing anyway
- Code simplicity is more valuable than 50ns

**Reconsider When**: Never - this optimization doesn't make sense for this use case.

---

### 4. Pipeline Behaviors / Middleware

**What**: Add MediatR-style pipeline behaviors for cross-cutting concerns (logging, caching, retry, validation).

**Potential Benefit**: AOP-style composition without modifying handlers.

**Why Deferred**:
- Significant complexity increase to the library
- Decorator pattern provides the same value with less library complexity
- Users can inherit from Mediator and override methods if needed

**Recommended Alternative** - Decorator Pattern:
```csharp
public class LoggingMediator(IMediator inner, ILogger logger) : IMediator
{
    public async Task<ICommandWorkflowResult> Send<TCommand>(
        TCommand command,
        CancellationToken cancellationToken = default) where TCommand : ICommand
    {
        logger.LogInformation("Sending {Command}", typeof(TCommand).Name);
        var result = await inner.Send(command, cancellationToken);
        logger.LogInformation("Completed {Command}: {Success}", typeof(TCommand).Name, result.Success);
        return result;
    }

    // Delegate other methods to inner...
}

// DI Registration
services.AddScoped<Mediator>();
services.AddScoped<IMediator>(sp =>
    new LoggingMediator(sp.GetRequiredService<Mediator>(), sp.GetRequiredService<ILogger>()));
```

**Reconsider When**: Multiple users request this and decorator pattern proves insufficient.

---

### 5. Async Local / Ambient Context

**What**: Provide ambient context (user ID, correlation ID, tenant ID) accessible from any handler without explicit parameter passing.

**Potential Benefit**: Cleaner handler signatures, less parameter threading.

**Why Deferred**:
- Encourages hidden dependencies and makes code harder to test
- Explicit constructor injection is clearer and more maintainable
- Can lead to subtle bugs when context doesn't flow correctly

**Recommended Alternative** - Explicit Injection:
```csharp
public class CreateOrderHandler(IMediator mediator, IUserContext userContext)
    : CommandHandler<CreateOrderCommand>(mediator)
{
    // userContext is explicit, testable, and discoverable
}
```

**Reconsider When**: Never - this is considered an anti-pattern.

---

### 6. Source Generators for Handler Registration

**What**: Auto-generate DI registration code for all handlers at compile time.

**Potential Benefit**: No manual registration, guaranteed all handlers registered.

**Why Deferred**:
- Each DI container has different registration patterns
- Adds massive build complexity
- Scrutor and similar libraries already solve this problem well
- Users can create their own conventions easily

**Recommended Alternative**:
```csharp
// Using Scrutor
services.Scan(scan => scan
    .FromAssemblyOf<CreateOrderHandler>()
    .AddClasses(classes => classes.AssignableTo(typeof(ICommandHandler<>)))
    .AsImplementedInterfaces()
    .WithScopedLifetime());
```

**Reconsider When**: If we add source generators for AOT support, registration could be included.

---

### 7. Polymorphic Event Handling

**What**: Allow handlers for base event types to also handle derived events.

**Example**:
```csharp
// Handler for IOrderEvent would also handle OrderCreatedEvent : IOrderEvent
public class AuditHandler : IEventHandler<IOrderEvent> { }
```

**Potential Benefit**: Cross-cutting event handling without explicit registration.

**Why Deferred**:
- Requires reflection or complex type analysis at runtime
- Adds ambiguity (which handlers run? in what order?)
- Performance cost for type hierarchy analysis
- Users can manually dispatch if needed

**Reconsider When**: Multiple users request this with clear use cases.

---

### 8. Span<T> for Event Handler Iteration

**What**: Use `Span<T>` instead of `IEnumerable<T>` for iterating event handlers.

**Potential Benefit**: 10-15% reduction in allocations for high-throughput event scenarios.

**Why Deferred**:
- Marginal improvement for typical use cases
- Requires restructuring how handlers are retrieved from DI
- Added complexity for minimal gain

**If Implemented**:
```csharp
private async Task Publish<TEvent>(TEvent @event, CancellationToken cancellationToken)
    where TEvent : IDomainEvent
{
    var handlers = GetEventHandlersAsSpan<TEvent>();
    for (var i = 0; i < handlers.Length; i++)
        await handlers[i].Handle(@event, cancellationToken).ConfigureAwait(false);
}
```

**Reconsider When**: Benchmarks show event handler iteration as a significant bottleneck.

---

## Comparison: AsyncMediator vs Alternatives

| Feature | AsyncMediator v3 | martinothamar/Mediator | MediatR |
|---------|------------------|------------------------|---------|
| Return Type | `Task<T>` | `ValueTask<T>` | `Task<T>` |
| AOT Support | No (cached reflection) | Yes (source gen) | No |
| Pipeline Behaviors | No (use decorators) | No | Yes |
| Dependencies | 0 | 0 | 3+ packages |
| Cancellation Tokens | Yes (v3.0) | Yes | Yes |
| Event Deferral | Built-in | Manual | Manual |
| Memory Allocations | Low | Minimal | Medium |
| License | MIT | MIT | Commercial* |

*MediatR moved to commercial license in 2025 (free for OSS).

---

## Decision Log

| Date | Decision | Rationale |
|------|----------|-----------|
| 2024-XX | Defer AOT support | Complexity vs. niche use case |
| 2024-XX | Keep Task over ValueTask | Misuse risk, handlers are async |
| 2024-XX | Add CancellationToken | v3 is breaking anyway, high value |
| 2024-XX | No pipeline behaviors | Decorator pattern is sufficient |

---

## Feedback

If you have use cases that would benefit from any of these deferred features, please open an issue with:
1. Your specific scenario
2. Why the recommended alternative doesn't work
3. Expected usage patterns

This helps prioritize future development.
