# Pipeline Behaviors Guide

## Overview

Pipeline behaviors wrap handler execution, enabling cross-cutting concerns without modifying handlers.

```
Request → Logging → Validation → UnitOfWork → Handler → Response
              ↑______________________________________________|
```

## Execution Order

**Logging → Validation → UnitOfWork → Handler**

| Order | Behavior | Why |
|-------|----------|-----|
| 1 | Logging | Captures everything including validation failures |
| 2 | Validation | Fails fast before expensive DB operations |
| 3 | UnitOfWork | Only opens transactions for validated requests |

---

## Behaviors

### LoggingBehavior

```csharp
using System.Diagnostics;
using Microsoft.Extensions.Logging;

public sealed class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var correlationId = Activity.Current?.Id ?? Guid.NewGuid().ToString();

        logger.LogInformation(
            "Processing {RequestName} [{CorrelationId}]",
            requestName, correlationId);

        var sw = Stopwatch.StartNew();
        try
        {
            var response = await next();
            sw.Stop();

            if (response is ICommandWorkflowResult { Success: false } result)
            {
                logger.LogWarning(
                    "Failed {RequestName} in {ElapsedMs}ms ({ErrorCount} errors) [{CorrelationId}]",
                    requestName, sw.ElapsedMilliseconds, result.ValidationResults.Count, correlationId);
            }
            else
            {
                logger.LogInformation(
                    "Completed {RequestName} in {ElapsedMs}ms [{CorrelationId}]",
                    requestName, sw.ElapsedMilliseconds, correlationId);
            }

            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Exception in {RequestName} after {ElapsedMs}ms [{CorrelationId}]",
                requestName, sw.ElapsedMilliseconds, correlationId);
            throw;
        }
    }
}
```

### ValidationBehavior

Uses built-in `DataAnnotations`. Complements (not replaces) handler's business validation.

```csharp
using System.ComponentModel.DataAnnotations;

public sealed class ValidationBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TResponse : ICommandWorkflowResult
{
    public Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var results = new List<ValidationResult>();
        var context = new System.ComponentModel.DataAnnotations.ValidationContext(request);

        if (!Validator.TryValidateObject(request, context, results, validateAllProperties: true))
        {
            var response = new CommandWorkflowResult(results);
            return Task.FromResult((TResponse)(ICommandWorkflowResult)response);
        }

        return next();
    }
}
```

### UnitOfWorkBehavior

Manages DbContext transactions. Skips queries automatically.

```csharp
using Microsoft.EntityFrameworkCore;

public sealed class UnitOfWorkBehavior<TRequest, TResponse>(AppDbContext db)
    : IPipelineBehavior<TRequest, TResponse>
    where TResponse : ICommandWorkflowResult
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Skip if query or already in transaction
        if (IsQuery() || db.Database.CurrentTransaction is not null)
            return await next();

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database
                .BeginTransactionAsync(cancellationToken);
            try
            {
                var response = await next();

                if (response.Success)
                {
                    await db.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                }
                else
                {
                    await transaction.RollbackAsync(cancellationToken);
                }

                return response;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    private static bool IsQuery() =>
        typeof(TRequest).Name.EndsWith("Query", StringComparison.Ordinal);
}
```

---

## Program.cs

```csharp
using AsyncMediator;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// DbContext
builder.Services.AddDbContext<AppDbContext>(opts =>
    opts.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

// AsyncMediator with source generator (handlers auto-discovered)
// Behaviors registered in order: first = outermost
builder.Services.AddAsyncMediator(cfg => cfg
    .AddOpenGenericBehavior(typeof(LoggingBehavior<,>))
    .AddOpenGenericBehavior(typeof(ValidationBehavior<,>))
    .AddOpenGenericBehavior(typeof(UnitOfWorkBehavior<,>)));

var app = builder.Build();

app.MapPost("/orders", async (CreateOrderCommand cmd, IMediator mediator, CancellationToken ct) =>
{
    var result = await mediator.Send(cmd, ct);
    return result.Success ? Results.Ok() : Results.BadRequest(result.ValidationResults);
});

app.Run();
```

---

## Example Command

```csharp
using System.ComponentModel.DataAnnotations;

// DataAnnotations for framework validation (handled by ValidationBehavior)
public sealed class CreateOrderCommand : ICommand
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string CustomerName { get; init; } = "";

    [Range(1, int.MaxValue)]
    public int Quantity { get; init; }
}

public sealed class CreateOrderHandler(IMediator mediator, AppDbContext db)
    : CommandHandler<CreateOrderCommand>(mediator)
{
    // Business validation (domain rules)
    protected override Task Validate(ValidationContext ctx, CancellationToken ct)
    {
        if (Command.Quantity > 1000)
            ctx.AddError(nameof(Command.Quantity), "Orders > 1000 require approval");
        return Task.CompletedTask;
    }

    protected override async Task<ICommandWorkflowResult> DoHandle(
        ValidationContext ctx, CancellationToken ct)
    {
        var order = new Order
        {
            CustomerName = Command.CustomerName,
            Quantity = Command.Quantity
        };

        db.Orders.Add(order);
        // No SaveChanges() - UnitOfWorkBehavior handles it

        Mediator.DeferEvent(new OrderCreatedEvent(order.Id));
        return CommandWorkflowResult.Ok();
    }
}
```

---

## Validation Strategy

| Layer | Responsibility | Example |
|-------|----------------|---------|
| DataAnnotations | Format, length, required | `[Required]`, `[StringLength]` |
| Handler.Validate() | Business rules | "Quantity > 1000 requires approval" |
| Domain entity | Invariants | Entity constructor guards |

---

## Gotchas

### 1. Don't Mix Transaction Approaches
Choose **one**:
- `UnitOfWorkBehavior` (recommended) - flexible, testable
- Handler's `UseTransactionScope => true` - legacy pattern

### 2. DbContext Lifetime
Mediator and DbContext must have matching lifetimes (both Scoped).

### 3. Behavior Type Constraints
`ValidationBehavior` and `UnitOfWorkBehavior` constrain `TResponse : ICommandWorkflowResult`.
Queries use different behaviors or none.

### 4. Behavior Caching
Factory is called once per request type, then cached. Behaviors must be thread-safe.

---

## Testing

```csharp
[Fact]
public async Task LoggingBehavior_LogsRequestName()
{
    var logger = new FakeLogger<LoggingBehavior<TestCommand, ICommandWorkflowResult>>();
    var behavior = new LoggingBehavior<TestCommand, ICommandWorkflowResult>(logger);

    await behavior.Handle(
        new TestCommand(),
        () => Task.FromResult<ICommandWorkflowResult>(CommandWorkflowResult.Ok()),
        CancellationToken.None);

    Assert.Contains("TestCommand", logger.Messages.First());
}
```

---

## File Structure

```
src/
├── Commands/
│   ├── CreateOrderCommand.cs
│   └── CreateOrderHandler.cs
├── Events/
│   ├── OrderCreatedEvent.cs
│   └── OrderCreatedHandler.cs
├── Infrastructure/
│   └── Behaviors/
│       ├── LoggingBehavior.cs
│       ├── ValidationBehavior.cs
│       └── UnitOfWorkBehavior.cs
└── Program.cs
```
