# AsyncMediator Architecture

A lightweight, high-performance mediator pattern implementation for .NET 9/10 with zero-allocation event deferral and minimal memory overhead.

## Design Philosophy

AsyncMediator is built on three core principles:

1. **Performance First**: Zero-allocation patterns, singleton caching, and minimal virtual dispatch
2. **Explicit Control**: TransactionScope is opt-in, not automatic
3. **Simple Abstractions**: Marker interfaces and factory delegates over heavyweight frameworks

## High-Level Architecture

```mermaid
graph TB
    Client[Client Code]

    subgraph "IMediator API"
        Send[Send Command]
        Query[Query Data]
        LoadList[LoadList]
        DeferEvent[DeferEvent]
        ExecuteDeferredEvents[ExecuteDeferredEvents]
    end

    subgraph "Handler Resolution"
        Factory[Factory]
        SingleFactory[SingleInstanceFactory Delegate]
        MultiFactory[MultiInstanceFactory Delegate]
        DI[DI Container]
    end

    subgraph "Handlers"
        CommandHandler[ICommandHandler]
        QueryHandler[IQuery]
        LookupHandler[ILookupQuery]
        EventHandler[IEventHandler]
    end

    subgraph "Infrastructure"
        CommandBase[CommandHandler Base Class]
        ValidationCtx[ValidationContext]
        WorkflowResult[CommandWorkflowResult]
        EventQueue[ConcurrentQueue Events]
    end

    Client --> Send
    Client --> Query
    Client --> LoadList
    Client --> DeferEvent

    Send --> Factory
    Query --> Factory
    LoadList --> Factory

    Factory --> SingleFactory
    Factory --> MultiFactory

    SingleFactory --> DI
    MultiFactory --> DI

    DI --> CommandHandler
    DI --> QueryHandler
    DI --> LookupHandler
    DI --> EventHandler

    CommandHandler -.inherits.- CommandBase
    CommandBase --> ValidationCtx
    CommandBase --> WorkflowResult
    CommandBase --> DeferEvent
    CommandBase --> ExecuteDeferredEvents

    DeferEvent --> EventQueue
    ExecuteDeferredEvents --> EventQueue
    ExecuteDeferredEvents --> EventHandler
```

## Core Components

### Factory Delegate Pattern

AsyncMediator uses function delegates instead of service locator anti-patterns. The DI container is accessed only at the mediator boundary, keeping handler code clean.

```csharp
public delegate object SingleInstanceFactory(Type serviceType);
public delegate IEnumerable<object> MultiInstanceFactory(Type serviceType);

internal class Factory(MultiInstanceFactory multi, SingleInstanceFactory single)
{
    public IEnumerable<T> CreateEnumerableOf<T>() where T : class =>
        multi(typeof(T)).Cast<T>();

    public T Create<T>() where T : class =>
        (T)single(typeof(T));
}
```

**Why this pattern:**
- Decouples from specific DI frameworks
- No service locator anti-pattern in handler code
- Easy testing: just provide mock factories
- Zero reflection overhead after handler resolution

### Command Flow

```mermaid
sequenceDiagram
    participant Client
    participant Mediator
    participant Factory
    participant Handler as CommandHandler
    participant ValidationCtx as ValidationContext
    participant EventQueue as Event Queue
    participant EventHandler

    Client->>Mediator: Send(command)
    Mediator->>Factory: Create<ICommandHandler<T>>()
    Factory-->>Mediator: handler instance
    Mediator->>Handler: Handle(command)

    Handler->>ValidationCtx: Validate(context)

    alt Validation Failed
        Handler-->>Mediator: CommandWorkflowResult(errors)
        Mediator-->>Client: result
    else Validation Passed
        Handler->>Handler: DoHandle(context)
        Handler->>Mediator: DeferEvent(event)
        Mediator->>EventQueue: Enqueue(type, event)

        alt UseTransactionScope = true
            Handler->>Handler: TransactionScope begin
            Handler->>Handler: transaction.Complete()
            Handler->>Handler: TransactionScope dispose (commit)
        end

        Handler->>Mediator: ExecuteDeferredEvents()

        loop For each deferred event
            Mediator->>EventQueue: TryDequeue()
            Mediator->>Factory: CreateEnumerableOf<IEventHandler<T>>()
            Factory-->>Mediator: event handlers

            loop For each handler
                Mediator->>EventHandler: Handle(event)
            end
        end

        Handler-->>Mediator: CommandWorkflowResult.Ok()
        Mediator-->>Client: result
    end
```

### Zero-Allocation Event Deferral

Event deferral avoids closure allocations by storing the event type and instance as a tuple.

```csharp
private readonly ConcurrentQueue<(Type EventType, object Event)> _deferredEvents = new();

public void DeferEvent<TEvent>(TEvent @event) where TEvent : IDomainEvent =>
    _deferredEvents.Enqueue((typeof(TEvent), @event!));
```

**Traditional approach (allocates closure):**
```csharp
_deferredEvents.Enqueue(() => Publish(@event));
```

**Our approach:**
- Stores `(Type, object)` tuple: 16 bytes on stack
- No closure allocation
- No captured variables
- Delegate cache eliminates reflection on hot path

### Expression-Compiled Publish Delegates

Reflection is expensive. AsyncMediator uses expression-compiled delegates cached per event type, eliminating reflection overhead entirely after first use.

```csharp
private static readonly ConcurrentDictionary<Type, Func<Mediator, object, CancellationToken, Task>> PublishDelegateCache = new();

public async Task ExecuteDeferredEvents(CancellationToken cancellationToken = default)
{
    while (_deferredEvents.TryDequeue(out var item))
    {
        var publishDelegate = PublishDelegateCache.GetOrAdd(item.EventType, CreatePublishDelegate);
        await publishDelegate(this, item.Event, cancellationToken).ConfigureAwait(false);
    }
}

private static Func<Mediator, object, CancellationToken, Task> CreatePublishDelegate(Type eventType)
{
    var mediatorParam = Expression.Parameter(typeof(Mediator), "mediator");
    var eventParam = Expression.Parameter(typeof(object), "event");
    var ctParam = Expression.Parameter(typeof(CancellationToken), "ct");

    var method = typeof(Mediator)
        .GetMethod(nameof(PublishBoxed), BindingFlags.Instance | BindingFlags.NonPublic)!
        .MakeGenericMethod(eventType);

    var call = Expression.Call(
        mediatorParam,
        method,
        Expression.Convert(eventParam, eventType),
        ctParam);

    return Expression.Lambda<Func<Mediator, object, CancellationToken, Task>>(
        call, mediatorParam, eventParam, ctParam).Compile();
}
```

**Why expression-compiled delegates:**
- `MethodInfo.Invoke()` allocates `object[]` on every call
- Expression-compiled delegates are true IL: zero per-call allocation
- ~45% faster than reflection-based invocation
- ~42% less memory per invocation

**Performance impact:**
- First event of each type: expression compilation cost (one-time, ~150μs)
- Subsequent events: dictionary lookup + direct call (nanoseconds)
- Cache is static: shared across all mediator instances

## Command Handler Base Class

The abstract `CommandHandler<TCommand>` base class provides structure and removes boilerplate.

```csharp
public abstract class CommandHandler<TCommand>(IMediator mediator) : ICommandHandler<TCommand>
    where TCommand : ICommand
{
    protected TCommand Command { get; private set; } = default!;
    protected IMediator Mediator { get; } = mediator;
    protected CancellationToken CancellationToken { get; private set; }
    protected virtual bool UseTransactionScope => false;

    public async Task<ICommandWorkflowResult> Handle(TCommand command, CancellationToken cancellationToken = default)
    {
        Command = command;
        CancellationToken = cancellationToken;
        var context = new ValidationContext();

        await Validate(context, cancellationToken).ConfigureAwait(false);
        if (context.ValidationResults.Count > 0)
            return new CommandWorkflowResult(context.ValidationResults);

        return UseTransactionScope
            ? await HandleWithTransaction(context, cancellationToken).ConfigureAwait(false)
            : await HandleWithoutTransaction(context, cancellationToken).ConfigureAwait(false);
    }

    protected abstract Task Validate(ValidationContext validationContext, CancellationToken cancellationToken);
    protected abstract Task<ICommandWorkflowResult> DoHandle(ValidationContext validationContext, CancellationToken cancellationToken);
}
```

**Key design decisions:**

1. **Validation first**: Validation errors short-circuit before `DoHandle`
2. **Transaction opt-in**: `UseTransactionScope` defaults to `false`
3. **Command property**: Available to all methods without parameter passing
4. **Mediator access**: Handlers can defer events and execute nested queries

## TransactionScope: Why Opt-In?

```csharp
protected virtual bool UseTransactionScope => false;
```

TransactionScope is **opt-in** because:

1. **Not all operations need ACID guarantees**: Read models, idempotent operations, or single-database writes don't need distributed transactions
2. **Performance cost**: TransactionScope has overhead even for local transactions
3. **Complexity**: Distributed transactions require MSDTC and careful configuration
4. **Explicit intent**: Transactions are important decisions that should be visible in code

**When to use TransactionScope:**
- Multi-database operations requiring atomicity
- Coordinating database writes with message queues
- Operations where partial success is unacceptable

**Example:**
```csharp
public class CreateOrderHandler(IMediator mediator) : CommandHandler<CreateOrderCommand>(mediator)
{
    protected override bool UseTransactionScope => true; // Multi-DB operation

    protected override Task Validate(ValidationContext ctx, CancellationToken ct) => Task.CompletedTask;

    protected override async Task<ICommandWorkflowResult> DoHandle(ValidationContext ctx, CancellationToken ct)
    {
        // Insert into Orders table
        // Insert into OrderItems table
        // Defer OrderCreated event
        // All-or-nothing: transaction will rollback if any step fails
        return CommandWorkflowResult.Ok();
    }
}
```

## Event Deferral Flow

```mermaid
sequenceDiagram
    participant Handler
    participant Mediator
    participant Queue as ConcurrentQueue
    participant Cache as PublishDelegateCache
    participant EventHandlers

    Note over Handler,Queue: Event Deferral Phase
    Handler->>Mediator: DeferEvent<OrderCreated>(event)
    Mediator->>Queue: Enqueue((typeof(OrderCreated), event))
    Note over Queue: Tuple stored: 16 bytes, no closure

    Handler->>Mediator: DeferEvent<InventoryReserved>(event)
    Mediator->>Queue: Enqueue((typeof(InventoryReserved), event))

    Note over Handler,EventHandlers: Event Execution Phase
    Handler->>Mediator: ExecuteDeferredEvents()

    loop Until queue empty
        Mediator->>Queue: TryDequeue()
        Queue-->>Mediator: (Type, object)

        Mediator->>Cache: GetOrAdd(Type, CreatePublishDelegate)

        alt First time for this event type
            Cache->>Cache: CreatePublishDelegate(Type)
            Note over Cache: Build delegate via reflection (one-time)
        end

        Cache-->>Mediator: Func<Mediator, object, Task>

        Mediator->>Mediator: publishDelegate(this, event)
        Mediator->>EventHandlers: Publish<TEvent>(event)

        loop For each IEventHandler<TEvent>
            Mediator->>EventHandlers: handler.Handle(event)
        end
    end
```

### Safe-by-Default Behavior

AsyncMediator is designed to make correct usage easy ("pit of success"):

**Event execution only on success:**
- Deferred events execute only when `DoHandle` returns a successful result (`result.Success == true`)
- If `DoHandle` returns a failure result (via `CommandWorkflowResult.ValidationResults`), events are not executed
- Validation errors in `ValidationContext` short-circuit before `DoHandle` is called

**TransactionScope safety:**
- When `UseTransactionScope` is enabled, event handlers run *after* `transaction.Complete()` succeeds
- This ensures external side effects (HTTP calls, emails) only occur after the transaction commits
- For event handlers that must participate in the transaction (e.g., writing to an event store), consider using an outbox pattern

**Automatic queue cleanup:**
- If a command throws an exception, the deferred event queue is automatically cleared
- This prevents events from one failed command leaking into subsequent commands
- Use `ClearDeferredEvents()` for manual control when needed

**Zero-allocation nuance:**
- "Zero allocation" for event deferral is achieved after the queue's internal segments are warmed and the delegate cache is populated
- First-time event types incur a one-time reflection and expression compilation cost (~150μs)

## Empty List Singleton Pattern

`CommandWorkflowResult` uses a singleton empty list to eliminate allocations for the success case.

```csharp
public class CommandWorkflowResult : ICommandWorkflowResult
{
    private static readonly List<ValidationResult> EmptyValidationResults = [];

    public CommandWorkflowResult() => ValidationResults = EmptyValidationResults;

    public List<ValidationResult> ValidationResults { get; protected set; }

    public bool Success => ValidationResults.Count == 0;

    public static CommandWorkflowResult Ok() => new();

    private void EnsureMutableList()
    {
        if (ReferenceEquals(ValidationResults, EmptyValidationResults))
            ValidationResults = [];
    }
}
```

**Why this works:**

1. Most commands succeed (no validation errors)
2. Success case: shares static empty list across all instances
3. Failure case: `EnsureMutableList()` allocates new list only when needed
4. Copy-on-write pattern: safe because list is never mutated while shared

**Memory impact:**
- Success case: 0 bytes allocated for `ValidationResults`
- Failure case: standard `List<ValidationResult>` allocation

## Query Patterns

AsyncMediator supports two query patterns:

### 1. Parameterized Query

```csharp
public interface IQuery<in TCriteria, TResult>
{
    Task<TResult> Query(TCriteria criteria, CancellationToken cancellationToken = default);
}

// Usage
var orders = await mediator.Query<OrderSearchCriteria, List<Order>>(criteria, ct);
```

### 2. Lookup Query (No Parameters)

```csharp
public interface ILookupQuery<TResult>
{
    Task<TResult> Query(CancellationToken cancellationToken = default);
}

// Usage
var countries = await mediator.LoadList<List<Country>>(ct);
```

**Why two patterns:**
- Lookup queries avoid unnecessary criteria objects for simple cases
- Type inference works better: `LoadList<T>()` vs `Query<EmptyCriteria, T>(null)`
- Clearer intent in handler registration

## DI Container Integration

AsyncMediator integrates with any DI container via factory delegates.

```mermaid
graph LR
    subgraph "Application Startup"
        Container[DI Container]
        Registration[Handler Registration]
    end

    subgraph "Factory Delegates"
        Single[SingleInstanceFactory]
        Multi[MultiInstanceFactory]
    end

    subgraph "Mediator"
        Factory[Factory]
        Mediator[Mediator]
    end

    subgraph "Runtime"
        Handler[Handler Instances]
    end

    Container --> Registration
    Registration --> Single
    Registration --> Multi

    Single --> Factory
    Multi --> Factory

    Factory --> Mediator

    Mediator --> Factory
    Factory --> Single
    Factory --> Multi
    Single --> Container
    Multi --> Container
    Container --> Handler
```

**Microsoft.Extensions.DependencyInjection example:**

```csharp
services.AddScoped<IMediator>(sp => new Mediator(
    serviceType => sp.GetServices(serviceType),
    serviceType => sp.GetRequiredService(serviceType)
));
```

**Castle Windsor example:**

```csharp
container.Register(Component.For<IMediator>()
    .UsingFactoryMethod(kernel => new Mediator(
        serviceType => kernel.ResolveAll(serviceType),
        serviceType => kernel.Resolve(serviceType)
    ))
);
```

## Source Generator Architecture

The `AsyncMediator.SourceGenerator` package provides compile-time handler discovery, eliminating the need for manual DI registration.

### Design Goals

1. **Zero-config**: Handlers are discovered automatically by implementing marker interfaces
2. **Compile-time validation**: Missing or duplicate handlers are detected at build time
3. **No reflection at runtime**: All handler types are known at compile time
4. **Incremental generation**: Only regenerates when handler code changes

### How It Works

```mermaid
graph TB
    subgraph "Build Time"
        Source[Source Code] --> Roslyn[Roslyn Compiler]
        Roslyn --> SG[HandlerDiscoveryGenerator]
        SG --> Analysis[Interface Analysis]

        Analysis --> Discovery[Handler Discovery]
        Discovery --> Attributes[AsyncMediatorAttributes.g.cs]
        Discovery --> Registration[AsyncMediatorRegistration.g.cs]
        Discovery --> Extensions[AsyncMediatorExtensions.g.cs]
        Discovery --> Builder[AsyncMediatorBuilder.g.cs]

        Roslyn --> Analyzer[AsyncMediatorAnalyzer]
        Analyzer --> Diagnostics[Compile-Time Diagnostics]
    end

    subgraph "Runtime"
        Registration --> AddAsyncMediator[AddAsyncMediator Method]
        Builder --> Config[Configuration]
        AddAsyncMediator --> DI[DI Container]
    end
```

### Generated Files

| File | Purpose |
|------|---------|
| `AsyncMediatorAttributes.g.cs` | Attributes for handler customization |
| `AsyncMediatorRegistration.g.cs` | Handler service descriptors |
| `AsyncMediatorExtensions.g.cs` | `AddAsyncMediator()` extension method |
| `AsyncMediatorBuilder.g.cs` | Fluent builder for configuration |

### Handler Discovery

The generator scans for classes implementing:
- `ICommandHandler<TCommand>`
- `IEventHandler<TEvent>`
- `IQuery<TCriteria, TResult>`
- `ILookupQuery<TResult>`

**Discovery criteria:**
- Must be non-abstract class
- Must not have `[ExcludeFromMediator]` attribute
- Must implement one of the handler interfaces

### Incremental Generation

The generator uses Roslyn's incremental generator API for efficient builds:

```csharp
public void Initialize(IncrementalGeneratorInitializationContext context)
{
    var handlerCandidates = context.SyntaxProvider
        .CreateSyntaxProvider(
            predicate: static (node, _) => IsPotentialHandler(node),
            transform: static (ctx, _) => GetHandlerInfo(ctx))
        .Where(static h => h.HasValue)
        .Select(static (h, _) => h!.Value);

    context.RegisterSourceOutput(handlerCandidates.Collect(), GenerateRegistrationCode);
}
```

**Performance benefits:**
- Only analyzes classes with base lists (potential handlers)
- Caches handler metadata between builds
- Only regenerates when handler signatures change

### Analyzer Diagnostics

The `AsyncMediatorAnalyzer` provides compile-time diagnostics:

| Code | Severity | Description |
|------|----------|-------------|
| ASYNCMED001 | Error | Command used via `Send()` but no handler registered |
| ASYNCMED002 | Error | Multiple handlers for the same command type |
| ASYNCMED003 | Warning | Query used via `Query()` but no handler registered |
| ASYNCMED004 | Warning | Event used via `Publish()` but no handler registered |
| ASYNCMED006 | Info | Message type marked with `[MediatorDraft]` |
| ASYNCMED007 | Warning | Multiple handlers for the same query signature |

### Generated Builder Pattern

The builder enables fluent configuration without service locator anti-patterns:

```csharp
// Generated code (simplified)
public sealed class AsyncMediatorBuilder(IServiceCollection services)
{
    public ServiceLifetime DefaultLifetime { get; set; } = ServiceLifetime.Scoped;
    public ServiceLifetime MediatorLifetime { get; set; } = ServiceLifetime.Scoped;
    internal bool HasBehaviors { get; private set; }

    public AsyncMediatorBuilder AddBehavior<TBehavior>(ServiceLifetime lifetime = ServiceLifetime.Transient)
        where TBehavior : class
    {
        services.Add(new ServiceDescriptor(typeof(TBehavior), typeof(TBehavior), lifetime));
        HasBehaviors = true;
        return this;
    }
}
```

**Key design decision**: The `BehaviorFactory` is created from the `IServiceProvider` at runtime, not during registration. This avoids the anti-pattern of calling `BuildServiceProvider()` during configuration.

### Extension Method Generation

```csharp
// Generated code (simplified)
public static IServiceCollection AddAsyncMediator(
    this IServiceCollection services,
    Action<AsyncMediatorBuilder>? configure = null)
{
    var builder = new AsyncMediatorBuilder(services);
    configure?.Invoke(builder);

    AddGeneratedHandlers(services, builder.DefaultLifetime);

    services.Add(new ServiceDescriptor(
        typeof(IMediator),
        sp => new Mediator(
            multiInstanceFactory: type => (IEnumerable<object>)sp.GetServices(type),
            singleInstanceFactory: type => sp.GetRequiredService(type),
            behaviors: null,
            behaviorFactory: builder.HasBehaviors
                ? type => (IEnumerable<object>)sp.GetServices(type)
                : null),
        builder.MediatorLifetime));

    return services;
}
```

**Why `HasBehaviors` check**: When no behaviors are registered, passing `null` for `behaviorFactory` enables the fast-path in the Mediator that skips behavior resolution entirely.

### Testing the Source Generator

Source generators require specialized testing:

```csharp
[TestMethod]
public void Generate_WithCommandHandler_RegistersCorrectly()
{
    var source = @"
        public class TestCommand : ICommand { }
        public class TestHandler : ICommandHandler<TestCommand> { ... }";

    var (generatedSources, diagnostics) = RunGenerator(source);

    Assert.IsTrue(generatedSources.Any(s => s.Contains("TestHandler")));
    Assert.IsTrue(generatedSources.Any(s => s.Contains("CommandHandlerCount = 1")));
}
```

## Performance Characteristics

Based on BenchmarkDotNet v0.14.0 (.NET 10.0, AMD Ryzen Threadripper 3960X):

### Memory Allocations

| Operation | Allocations | Notes |
|-----------|-------------|-------|
| `Send<TCommand>` (success) | ~488 B | Handler + context + result |
| `Send<TCommand>` (validation error) | ~632 B | Adds mutable `List<ValidationResult>` |
| `DeferEvent<TEvent>` | 0 B | Tuple stored in concurrent queue |
| `ExecuteDeferredEvents` | 0 B* | *After publish delegates cached |
| `Query<TCriteria, TResult>` | ~248-776 B | Depends on criteria complexity |
| `ValidationContext` creation | 32 B | Minimal overhead |
| `CommandWorkflowResult.Ok()` | 0 B | Uses singleton empty list |

### Latency

| Scenario | Mean | Notes |
|----------|------|-------|
| Send (single command) | 163 ns | Baseline operation |
| Send (10 concurrent) | 1.6 μs | Linear scaling |
| Query (complex criteria) | 105 ns | Includes handler resolution |
| LoadList (no criteria) | 82 ns | Fastest query path |
| DeferEvent (single) | 575 ns | Zero allocation |
| DeferEvent (1000 bulk) | 41 μs | ~41 ns per event |

### Pipeline Behavior Overhead

| Configuration | Mean | Memory | Overhead |
|--------------|------|--------|----------|
| No behaviors | 185 ns | 416 B | Baseline |
| Empty behaviors `[]` | 173 ns | 416 B | **0%** |
| 1 behavior | 265 ns | 688 B | +43% |
| 3 behaviors | 276 ns | 896 B | +49% |

The empty behaviors array has **identical** performance to not passing behaviors at all.

### Scalability Characteristics

- **Command handlers**: O(1) resolution via DI container
- **Event handlers**: O(n) where n = number of handlers per event
- **Event queue**: Lock-free `ConcurrentQueue`, scales to high concurrency
- **Publish delegate cache**: `ConcurrentDictionary`, thread-safe with minimal contention

## Design Decisions

### 1. List\<T\> vs IList\<T\>

```csharp
public List<ValidationResult> ValidationResults { get; protected set; }
```

**Rationale:**
- `IList<T>` uses virtual dispatch for `Count` and indexer
- `List<T>` is sealed: JIT inlines calls
- Validation checks (`if (context.ValidationResults.Count > 0)`) are hot path
- Concrete type: 10-15% faster in tight loops

### 2. ConcurrentQueue for Events

```csharp
private readonly ConcurrentQueue<(Type EventType, object Event)> _deferredEvents = new();
```

**Rationale:**
- Lock-free: no thread contention
- FIFO order: events execute in deferral order
- `TryDequeue`: predictable performance
- No blocking: safe for async handlers

### 3. Static Delegate Cache

```csharp
private static readonly ConcurrentDictionary<Type, Func<Mediator, object, Task>> PublishDelegateCache = new();
```

**Rationale:**
- Static: shared across all mediator instances
- Event types are stable: cache hit rate > 99.9%
- One-time reflection cost per event type
- Thread-safe without locks

### 4. ConfigureAwait(false) Everywhere

```csharp
await handler.Handle(@event).ConfigureAwait(false);
```

**Rationale:**
- AsyncMediator is infrastructure: doesn't need synchronization context
- Reduces allocations: no context capture
- Better performance in ASP.NET: doesn't post back to request thread
- Safe: mediator doesn't access UI or request-specific state

## Extension Points

AsyncMediator is designed for extension without modification:

### Custom Command Result Types

```csharp
public class CommandWorkflowResult<T> : CommandWorkflowResult
{
    public T? TypedResult { get; set; }
}
```

### Pre/Post Processing

```csharp
public class LoggingMediator(MultiInstanceFactory multi, SingleInstanceFactory single)
    : Mediator(multi, single)
{
    public override async Task<ICommandWorkflowResult> Send<TCommand>(TCommand command)
    {
        Log($"Executing {typeof(TCommand).Name}");
        var result = await base.Send(command);
        Log($"Completed {typeof(TCommand).Name}: {result.Success}");
        return result;
    }
}
```

### Custom Validation

```csharp
public abstract class FluentValidationCommandHandler<TCommand>(IMediator mediator, IValidator<TCommand> validator)
    : CommandHandler<TCommand>(mediator)
{
    protected override async Task Validate(ValidationContext context, CancellationToken cancellationToken)
    {
        var result = await validator.ValidateAsync(Command, cancellationToken);
        if (!result.IsValid)
            context.AddErrors(result.Errors.Select(e =>
                new ValidationResult(e.ErrorMessage, new[] { e.PropertyName })));
    }

    protected override Task<ICommandWorkflowResult> DoHandle(ValidationContext ctx, CancellationToken ct) =>
        Task.FromResult<ICommandWorkflowResult>(CommandWorkflowResult.Ok());
}
```

## Pipeline Behaviors

Pipeline behaviors wrap handler execution, enabling cross-cutting concerns (logging, caching, validation) without modifying handlers.

### Design Goals

1. **Zero-cost when unused**: No overhead when no behaviors are registered
2. **Opt-in**: Behaviors are passed to the Mediator constructor
3. **Composable**: Multiple behaviors wrap handlers like middleware
4. **Type-safe**: Behaviors are generic over request and response types

### Core Interface

```csharp
public delegate Task<TResponse> RequestHandlerDelegate<TResponse>();

public interface IPipelineBehavior<in TRequest, TResponse>
{
    Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken);
}
```

### Registration

```csharp
services.AddScoped<IMediator>(sp => new Mediator(
    type => sp.GetServices(type),
    type => sp.GetRequiredService(type),
    behaviors: [
        new LoggingBehavior<CreateOrderCommand, ICommandWorkflowResult>(),
        new ValidationBehavior<CreateOrderCommand, ICommandWorkflowResult>()
    ]));
```

### Execution Flow

```mermaid
sequenceDiagram
    participant Client
    participant Mediator
    participant Behavior1 as LoggingBehavior
    participant Behavior2 as ValidationBehavior
    participant Handler

    Client->>Mediator: Send(command)
    Mediator->>Behavior1: Handle(command, next)
    Note over Behavior1: Log "Starting..."
    Behavior1->>Behavior2: next()
    Note over Behavior2: Validate command
    Behavior2->>Handler: next()
    Handler-->>Behavior2: result
    Note over Behavior2: Check result
    Behavior2-->>Behavior1: result
    Note over Behavior1: Log "Completed"
    Behavior1-->>Mediator: result
    Mediator-->>Client: result
```

### Example Behaviors

**Logging Behavior:**
```csharp
public class LoggingBehavior<TRequest, TResponse>(ILogger logger)
    : IPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {RequestType}", typeof(TRequest).Name);
        var response = await next();
        logger.LogInformation("Handled {RequestType}", typeof(TRequest).Name);
        return response;
    }
}
```

**Exception Handling Behavior:**
```csharp
public class ExceptionHandlingBehavior<TRequest> : IPipelineBehavior<TRequest, ICommandWorkflowResult>
{
    public async Task<ICommandWorkflowResult> Handle(
        TRequest request,
        RequestHandlerDelegate<ICommandWorkflowResult> next,
        CancellationToken cancellationToken)
    {
        try
        {
            return await next();
        }
        catch (Exception ex)
        {
            return new CommandWorkflowResult([
                new ValidationResult($"Unexpected error: {ex.Message}")
            ]);
        }
    }
}
```

### Zero-Cost Implementation

The pipeline uses a fast-path check to skip behavior processing entirely when no behaviors are registered:

```csharp
public async Task<ICommandWorkflowResult> Send<TCommand>(TCommand command, CancellationToken cancellationToken)
    where TCommand : ICommand
{
    if (_pipeline.IsEmpty)  // Fast path: no overhead
        return await GetCommandHandler<TCommand>().Handle(command, cancellationToken);

    return await _pipeline.Execute<TCommand, ICommandWorkflowResult>(
        command,
        () => GetCommandHandler<TCommand>().Handle(command, cancellationToken),
        cancellationToken);
}
```

**Benchmark results:**
| Configuration | Mean | Memory | Overhead |
|--------------|------|--------|----------|
| No behaviors (baseline) | 138.6 ns | 416 B | - |
| Empty behaviors `[]` | 137.7 ns | 416 B | **0%** |
| 1 behavior | 193.3 ns | 688 B | +40% |
| 3 behaviors | 242.1 ns | 896 B | +75% |

The empty behaviors array has **identical** performance to not passing behaviors at all.

## When to Use AsyncMediator

**Good fit:**
- CQRS architectures
- Clean separation between commands and queries
- Domain event patterns
- Decoupling handlers from infrastructure
- Testing: easy to mock `IMediator`

**Not a fit:**
- Simple CRUD without validation
- Direct repository access is clearer
- Event sourcing (consider specialized frameworks)
- Real-time event processing (use messaging systems)

## Comparison with MediatR

| Feature | AsyncMediator | MediatR |
|---------|---------------|---------|
| Pipeline behaviors | Yes (opt-in, zero-cost) | Yes |
| Notifications | Via `IDomainEvent` | Via `INotification` |
| Event deferral | Built-in | Manual |
| TransactionScope | Opt-in | N/A |
| Validation base class | Included | Separate library |
| Memory allocations | Minimal | Higher |
| Runtime dependencies | Zero | Microsoft.Extensions.DI |

AsyncMediator now provides pipeline behaviors with a zero-cost opt-in design: when no behaviors are registered, performance is identical to having no pipeline support at all. Choose MediatR for a mature ecosystem with extensive community behaviors. Choose AsyncMediator if you want zero runtime dependencies, maximum performance, and built-in event deferral.

## Concurrency Model

### Thread Safety

- **Mediator**: Thread-safe, designed for scoped lifetime per request
- **Event queue**: `ConcurrentQueue` is lock-free
- **Delegate cache**: `ConcurrentDictionary` is thread-safe
- **Handlers**: Expected to be stateless or scoped

### Event Ordering

Events are processed in FIFO order within a single command handler execution:

```csharp
Mediator.DeferEvent(new Event1()); // Executes first
Mediator.DeferEvent(new Event2()); // Executes second
Mediator.DeferEvent(new Event3()); // Executes third
await Mediator.ExecuteDeferredEvents();
```

**Cross-handler ordering:** No guarantees. If `Event1Handler` defers `Event4`, execution order is:
1. Event1
2. Event2
3. Event3
4. Event4 (deferred during Event1 handling)

### Parallel Event Execution

Events execute serially by design:

```csharp
foreach (var eventHandler in GetEventHandlers<TEvent>())
    await eventHandler.Handle(@event).ConfigureAwait(false);
```

**Rationale:**
- Predictable: handlers execute in registration order
- Transactional: easier to reason about rollback
- Simple: no race conditions between handlers

For parallel event processing, use a message bus (RabbitMQ, Azure Service Bus).

## Testing Strategies

### Unit Testing Handlers

```csharp
[Fact]
public async Task CreateOrder_ValidCommand_ReturnsSuccess()
{
    var mediator = new Mock<IMediator>();
    var handler = new CreateOrderHandler(mediator.Object);
    var command = new CreateOrderCommand { CustomerId = 123 };

    var result = await handler.Handle(command);

    Assert.True(result.Success);
    mediator.Verify(m => m.DeferEvent(It.IsAny<OrderCreatedEvent>()), Times.Once);
}
```

### Integration Testing with Real Mediator

```csharp
[Fact]
public async Task CreateOrder_FullPipeline_PublishesEvents()
{
    var services = new ServiceCollection();
    services.AddScoped<IMediator>(sp => new Mediator(/* ... */));
    services.AddScoped<ICommandHandler<CreateOrderCommand>, CreateOrderHandler>();
    services.AddScoped<IEventHandler<OrderCreatedEvent>, OrderCreatedEventHandler>();

    var provider = services.BuildServiceProvider();
    var mediator = provider.GetRequiredService<IMediator>();

    var result = await mediator.Send(new CreateOrderCommand());

    Assert.True(result.Success);
    // Verify side effects in event handlers
}
```

### Testing Event Deferral

```csharp
[Fact]
public async Task DeferredEvents_ExecuteInOrder()
{
    var executionOrder = new List<string>();
    var registry = new TestHandlerRegistry();
    registry.RegisterMultiple<IEventHandler<TestEvent>>(new[]
    {
        new TestEventHandler1(executionOrder),
        new TestEventHandler2(executionOrder)
    });

    var mediator = new Mediator(registry.MultiInstanceFactory, registry.SingleInstanceFactory);

    mediator.DeferEvent(new TestEvent { Id = 1 });
    mediator.DeferEvent(new TestEvent { Id = 2 });
    await mediator.ExecuteDeferredEvents();

    Assert.Equal(new[] { "Handler1-Event1", "Handler2-Event1", "Handler1-Event2", "Handler2-Event2" },
                 executionOrder);
}
```

## References

- Factory delegate pattern: avoids service locator anti-pattern
- ConcurrentQueue: [System.Collections.Concurrent](https://learn.microsoft.com/en-us/dotnet/api/system.collections.concurrent.concurrentqueue-1)
- TransactionScope: [System.Transactions](https://learn.microsoft.com/en-us/dotnet/api/system.transactions.transactionscope)
- ConfigureAwait: [Best practices in asynchronous programming](https://learn.microsoft.com/en-us/archive/msdn-magazine/2013/march/async-await-best-practices-in-asynchronous-programming)
