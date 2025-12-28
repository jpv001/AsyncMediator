# AsyncMediator v3.x Migration Guide

This guide helps you migrate from AsyncMediator v2.x (.NET Standard 2.0) to v3.x (.NET 9/10).

## Breaking Changes

### 1. .NET 9 or .NET 10 Required

**Before (v2.x):**
```xml
<TargetFramework>netstandard2.0</TargetFramework>
```

**After (v3.0.0):**
```xml
<TargetFramework>net9.0</TargetFramework>
<!-- or -->
<TargetFramework>net10.0</TargetFramework>
```

**Migration:** Update your project to target .NET 9 or later. Both frameworks are fully supported with identical functionality and performance.

---

### 2. HandlerOrderAttribute Removed

The `[HandlerOrder(n)]` attribute and `OrderByExecutionOrder()` extension method have been removed.

**Reason:**
- The attribute relied on reflection, adding overhead to every event execution
- Handler ordering was never guaranteed in concurrent scenarios due to `ConcurrentBag` behavior
- The feature was already marked `[Obsolete]` since v2.0

**Before (v2.x):**
```csharp
[HandlerOrder(1)]
public class FirstHandler : IEventHandler<MyEvent>
{
    public Task Handle(MyEvent @event) { /* executes first */ }
}

[HandlerOrder(2)]
public class SecondHandler : IEventHandler<MyEvent>
{
    public Task Handle(MyEvent @event) { /* executes second */ }
}
```

**After (v3.0.0) - Option 1: DI Registration Order**
```csharp
// Handlers execute in registration order
services.AddTransient<IEventHandler<MyEvent>, FirstHandler>();
services.AddTransient<IEventHandler<MyEvent>, SecondHandler>();
```

**After (v3.0.0) - Option 2: Event Chaining (Recommended)**
```csharp
// FirstHandler defers SecondEvent after completing its work
public class FirstHandler : IEventHandler<MyEvent>
{
    private readonly IMediator _mediator;

    public FirstHandler(IMediator mediator) => _mediator = mediator;

    public Task Handle(MyEvent @event)
    {
        // First handler logic
        _mediator.DeferEvent(new SecondEvent(@event.Data));
        return Task.CompletedTask;
    }
}

public class SecondHandler : IEventHandler<SecondEvent>
{
    public Task Handle(SecondEvent @event)
    {
        // Second handler logic (guaranteed to run after FirstHandler)
        return Task.CompletedTask;
    }
}
```

---

### 3. Event Execution Order Changed

**Before (v2.x):** Events deferred via `ConcurrentBag` had non-deterministic ordering in multi-threaded scenarios.

**After (v3.0.0):** Events deferred via `ConcurrentQueue` execute in FIFO (First-In-First-Out) order.

**Impact:** Your event handlers may execute in a different order than before. This is a more predictable behavior.

---

## New Features

### Nullable Reference Types

All interfaces and classes now have proper nullable annotations:

```csharp
// Result<T> now correctly returns TResult? when no result is set
TResult? Result<TResult>() where TResult : class, new();
```

---

### Modern C# Patterns

The codebase uses modern C# 13 features for improved readability and performance:

- File-scoped namespaces
- Primary constructors
- Collection expressions
- Expression-bodied members
- Pattern matching

---

## Performance Improvements

| Metric | v2.x (Baseline) | v3.0.0 | Improvement |
|--------|-----------------|--------|-------------|
| Single Command | 1.36 μs | 1.02 μs | 25% faster |
| Concurrent Commands (10) | 15.6 μs | 10.5 μs | 33% faster |
| Handler Resolution | Reflection-based | Direct | Reflection removed |
| Memory per Command | 1.16 KB | 1.16 KB | No change |

---

## Step-by-Step Migration

### Step 1: Update Target Framework

```xml
<PropertyGroup>
  <TargetFramework>net9.0</TargetFramework>  <!-- or net10.0 -->
  <Nullable>enable</Nullable>
</PropertyGroup>
```

### Step 2: Remove HandlerOrderAttribute References

Search your codebase for:
- `[HandlerOrder(`
- `using AsyncMediator;` with HandlerOrderAttribute

Remove all usages and implement DI registration order or event chaining instead.

### Step 3: Update NuGet Package

```bash
dotnet add package AsyncMediator
dotnet add package AsyncMediator.SourceGenerator  # Recommended
```

### Step 4: Handle Nullable Warnings

If you call `Result<T>()`, be aware it can return `null`:

```csharp
// Before
var result = commandResult.Result<MyResult>();
Console.WriteLine(result.Value); // Possible NullReferenceException

// After
var result = commandResult.Result<MyResult>();
if (result is not null)
{
    Console.WriteLine(result.Value);
}

// Or use null-forgiving if you've validated Success
if (commandResult.Success)
{
    var result = commandResult.Result<MyResult>()!;
    Console.WriteLine(result.Value);
}
```

### Step 5: Test Your Event Handlers

Since event ordering may change, verify your event handlers work correctly:

1. Run your existing test suite
2. Pay attention to handlers that depend on execution order
3. Refactor to event chaining if explicit ordering is required

---

## Versioning Strategy

| Version | Target Framework | Status |
|---------|------------------|--------|
| v2.x | .NET Standard 2.0 | Maintenance (security fixes only) |
| v3.x | .NET 9, .NET 10 | Active development |

---

## Getting Help

If you encounter issues during migration:

1. Check this guide for common scenarios
2. Review the [Architecture documentation](ARCHITECTURE.md) for design decisions
3. Open an issue on GitHub for unresolved problems

---

## Changelog

### v3.x (Current)

**Breaking changes from v2.x:**
- Requires .NET 9 or .NET 10
- CancellationToken parameter added to all async interfaces (see section 5)
- Removed `HandlerOrderAttribute` and `OrderByExecutionOrder()`
- Event execution uses `ConcurrentQueue` (FIFO ordering)
- `TransactionScope` is opt-in (see section 4)
- `ICommandWorkflowResult.ValidationResults` is `List<T>` (was `IList<T>`)

**New features:**
- Source generator for automatic handler discovery (`AddAsyncMediator()`)
- Pipeline behaviors with zero-cost opt-in design
- Nullable reference type annotations
- Modern C# patterns throughout

---

### 4. TransactionScope Now Opt-In

**Before (v2.x):** All command handlers automatically wrapped execution in a `TransactionScope`.

**After (v3.0.0):** `TransactionScope` is disabled by default for maximum performance.

**Impact:** If your handlers rely on distributed transactions, you must opt-in.

**Migration:**

```csharp
public class MyTransactionalHandler : CommandHandler<MyCommand>
{
    public MyTransactionalHandler(IMediator mediator) : base(mediator) { }

    // Opt-in to TransactionScope when ACID compliance is required
    protected override bool UseTransactionScope => true;

    protected override Task Validate(ValidationContext context) => Task.CompletedTask;

    protected override Task<ICommandWorkflowResult> DoHandle(ValidationContext context)
    {
        // Your transactional logic here
        return Task.FromResult<ICommandWorkflowResult>(CommandWorkflowResult.Ok());
    }
}
```

**Rationale:** `TransactionScope` with `TransactionScopeAsyncFlowOption.Enabled` is one of the most expensive operations in .NET. Making it opt-in results in **84% performance improvement** for handlers that don't need distributed transactions.

---

### 5. CancellationToken Support Added

**After (v3.0.0):** All async methods now include an optional `CancellationToken` parameter for graceful cancellation support.

**Impact:** If you have **custom implementations** of any AsyncMediator interfaces, you must update the method signatures.

#### ICommandHandler

**Before (v2.x):**
```csharp
public class MyCommandHandler : ICommandHandler<MyCommand>
{
    public Task<ICommandWorkflowResult> Handle(MyCommand command)
    {
        // Implementation
    }
}
```

**After (v3.0.0):**
```csharp
public class MyCommandHandler : ICommandHandler<MyCommand>
{
    public Task<ICommandWorkflowResult> Handle(MyCommand command, CancellationToken cancellationToken = default)
    {
        // Implementation - use cancellationToken for async operations
    }
}
```

#### CommandHandler Base Class

**Before (v2.x):**
```csharp
public class MyHandler : CommandHandler<MyCommand>
{
    protected override Task Validate(ValidationContext ctx) { }
    protected override Task<ICommandWorkflowResult> DoHandle(ValidationContext ctx) { }
}
```

**After (v3.0.0):**
```csharp
public class MyHandler : CommandHandler<MyCommand>
{
    protected override Task Validate(ValidationContext ctx, CancellationToken cancellationToken) { }
    protected override Task<ICommandWorkflowResult> DoHandle(ValidationContext ctx, CancellationToken cancellationToken) { }
}
```

#### IEventHandler

**Before (v2.x):**
```csharp
public class MyEventHandler : IEventHandler<MyEvent>
{
    public Task Handle(MyEvent @event) { }
}
```

**After (v3.0.0):**
```csharp
public class MyEventHandler : IEventHandler<MyEvent>
{
    public Task Handle(MyEvent @event, CancellationToken cancellationToken = default) { }
}
```

#### IQuery and ILookupQuery

**Before (v2.x):**
```csharp
public class MyQuery : IQuery<MyCriteria, MyResult>
{
    public Task<MyResult> Query(MyCriteria criteria) { }
}

public class MyLookup : ILookupQuery<MyResult>
{
    public Task<MyResult> Query() { }
}
```

**After (v3.0.0):**
```csharp
public class MyQuery : IQuery<MyCriteria, MyResult>
{
    public Task<MyResult> Query(MyCriteria criteria, CancellationToken cancellationToken = default) { }
}

public class MyLookup : ILookupQuery<MyResult>
{
    public Task<MyResult> Query(CancellationToken cancellationToken = default) { }
}
```

#### Using CancellationToken

The `CommandHandler<T>` base class exposes a `CancellationToken` property for use in derived classes:

```csharp
public class LongRunningHandler : CommandHandler<ProcessDataCommand>
{
    protected override async Task<ICommandWorkflowResult> DoHandle(ValidationContext ctx, CancellationToken cancellationToken)
    {
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ProcessItem(item, cancellationToken);
        }
        return CommandWorkflowResult.Ok();
    }
}
```

**Migration Steps:**

1. Search for `: ICommandHandler<`, `: IEventHandler<`, `: IQuery<`, `: ILookupQuery<`
2. Add `CancellationToken cancellationToken = default` to all Handle/Query methods
3. Search for `: CommandHandler<`
4. Update `Validate` and `DoHandle` signatures to include `CancellationToken cancellationToken`
5. Propagate the cancellation token to any async operations within your handlers

**Rationale:** Cancellation support enables graceful shutdown, request timeout handling, and better resource management in web applications.
