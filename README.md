# AsyncMediator

A lightweight, high-performance mediator for .NET 9/10. Zero runtime dependencies. Minimal allocations.

## What is a Mediator?

A mediator decouples the "what" from the "how" in your application. Instead of controllers calling services directly, they send messages through a mediator that routes them to the right handler.

```
Controller → Mediator → Handler → Database/Services
```

This indirection enables clean architecture, testability, and cross-cutting concerns (logging, validation, caching) without polluting your business logic.

## When to Use AsyncMediator

**Great for:**
- CQRS architectures (separate read/write paths)
- Domain-driven design with domain events
- Decoupling handlers from HTTP/messaging infrastructure
- Adding cross-cutting behaviors without modifying handlers
- Applications where testability matters

**Not a fit:**
- Simple CRUD where direct repository access is clearer
- Real-time event streaming (use a message bus)
- Event sourcing (use specialized frameworks)

## Quick Start

### Install

```
dotnet add package AsyncMediator
```

### 1. Create a Command and Handler

```csharp
// The command (what you want to do)
public record CreateOrderCommand(Guid CustomerId, List<OrderItem> Items) : ICommand;

// The handler (how it's done)
public class CreateOrderHandler(IMediator mediator, IOrderRepository repo)
    : CommandHandler<CreateOrderCommand>(mediator)
{
    protected override Task Validate(ValidationContext ctx, CancellationToken ct)
    {
        if (Command.Items.Count == 0)
            ctx.AddError(nameof(Command.Items), "Order must have items");
        return Task.CompletedTask;
    }

    protected override async Task<ICommandWorkflowResult> DoHandle(ValidationContext ctx, CancellationToken ct)
    {
        var order = await repo.Create(Command.CustomerId, Command.Items, ct);
        return CommandWorkflowResult.Ok();
    }
}
```

### 2. Wire Up DI

```csharp
services.AddScoped<IMediator>(sp => new Mediator(
    type => sp.GetServices(type),
    type => sp.GetRequiredService(type)));

services.AddTransient<ICommandHandler<CreateOrderCommand>, CreateOrderHandler>();
```

### 3. Send Commands

```csharp
public class OrderController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateOrderCommand command, CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);
        return result.Success ? Ok() : BadRequest(result.ValidationResults);
    }
}
```

That's it. You're running.

## Queries

For read operations, use queries instead of commands:

```csharp
public record OrderSearchCriteria(Guid? CustomerId);

public class OrderQuery(IOrderRepository repo) : IQuery<OrderSearchCriteria, List<Order>>
{
    public Task<List<Order>> Query(OrderSearchCriteria c, CancellationToken ct) =>
        repo.Search(c.CustomerId, ct);
}

// Usage
var orders = await mediator.Query<OrderSearchCriteria, List<Order>>(criteria, ct);
```

## Events

Defer side effects until after your main operation completes:

```csharp
public record OrderCreatedEvent(Guid OrderId) : IDomainEvent;

// In your command handler
protected override async Task<ICommandWorkflowResult> DoHandle(ValidationContext ctx, CancellationToken ct)
{
    var order = await repo.Create(Command.CustomerId, Command.Items, ct);
    Mediator.DeferEvent(new OrderCreatedEvent(order.Id));  // Queued, not executed yet
    return CommandWorkflowResult.Ok();
}

// Event handler (executed after DoHandle completes)
public class SendConfirmationEmailHandler(IEmailService email) : IEventHandler<OrderCreatedEvent>
{
    public Task Handle(OrderCreatedEvent e, CancellationToken ct) =>
        email.SendOrderConfirmation(e.OrderId, ct);
}
```

**Safe by default:** Deferred events only execute when `DoHandle` returns a successful result. If the command fails (validation errors or `result.Success == false`), events are automatically skipped. When `UseTransactionScope` is enabled, events execute *after* the transaction commits successfully.

## Performance

| Operation | Latency | Memory |
|-----------|---------|--------|
| Send command | ~163 ns | ~488 B |
| Query | ~105 ns | ~248 B |
| Defer event | ~575 ns | 0 B |

Pipeline behaviors add zero overhead when no `behaviorFactory` is provided.

## Advanced Usage

### Source Generator (Recommended)

Automatically discover and register all handlers at compile time:

```
dotnet add package AsyncMediator.SourceGenerator
```

```csharp
// Zero-config: handlers auto-discovered
services.AddAsyncMediator();

// With behaviors
services.AddAsyncMediator(cfg => cfg
    .AddOpenGenericBehavior(typeof(LoggingBehavior<,>))
    .AddOpenGenericBehavior(typeof(ValidationBehavior<,>)));
```

### Manual Registration (No Source Generator)

If you prefer explicit control or can't use source generators:

```csharp
// Register handlers manually
services.AddTransient<ICommandHandler<CreateOrderCommand>, CreateOrderHandler>();
services.AddTransient<IQuery<OrderSearchCriteria, List<Order>>, OrderQuery>();

// Register behaviors
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));

// Wire up mediator with behavior factory
services.AddScoped<IMediator>(sp => new Mediator(
    multiInstanceFactory: type => sp.GetServices(type),
    singleInstanceFactory: type => sp.GetRequiredService(type),
    behaviorFactory: type => sp.GetServices(type)));  // Resolves behaviors from DI
```

### Pipeline Behaviors

Behaviors wrap handler execution for cross-cutting concerns. They execute in registration order, like middleware.

**Logging Behavior:**

```csharp
public class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        logger.LogInformation("Handling {Request}", typeof(TRequest).Name);
        var sw = Stopwatch.StartNew();
        var response = await next();
        logger.LogInformation("Handled {Request} in {Elapsed}ms", typeof(TRequest).Name, sw.ElapsedMilliseconds);
        return response;
    }
}
```

**Validation with DataAnnotations:**

```csharp
public record CreateOrderCommand(
    [property: Required] Guid CustomerId,
    [property: Required, MinLength(1)] List<OrderItem> Items) : ICommand;

public class ValidationBehavior<TRequest> : IPipelineBehavior<TRequest, ICommandWorkflowResult>
    where TRequest : ICommand
{
    public Task<ICommandWorkflowResult> Handle(
        TRequest request, RequestHandlerDelegate<ICommandWorkflowResult> next, CancellationToken ct)
    {
        var context = new System.ComponentModel.DataAnnotations.ValidationContext(request);
        var results = new List<ValidationResult>();

        if (!Validator.TryValidateObject(request, context, results, validateAllProperties: true))
            return Task.FromResult<ICommandWorkflowResult>(new CommandWorkflowResult(results));

        return next();
    }
}
```

**Unit of Work Behavior:**

```csharp
public class UnitOfWorkBehavior<TRequest>(IUnitOfWork uow)
    : IPipelineBehavior<TRequest, ICommandWorkflowResult>
    where TRequest : ICommand
{
    public async Task<ICommandWorkflowResult> Handle(
        TRequest request, RequestHandlerDelegate<ICommandWorkflowResult> next, CancellationToken ct)
    {
        var result = await next();
        if (result.Success)
            await uow.CommitAsync(ct);
        return result;
    }
}
```

### TransactionScope

Opt-in for operations requiring ACID guarantees:

```csharp
public class TransferFundsHandler(IMediator mediator) : CommandHandler<TransferFundsCommand>(mediator)
{
    protected override bool UseTransactionScope => true;  // Wraps DoHandle in TransactionScope
}
```

## Documentation

- [Architecture & Design Decisions](ARCHITECTURE.md)
- [Migration Guide (v2 → v3)](MIGRATION_GUIDE.md)
- [GitHub Issues](https://github.com/preemajames/AsyncMediator/issues)

## Breaking Changes (v3.0)

- Requires .NET 9 or .NET 10
- `CancellationToken` added to all async interfaces
- `TransactionScope` now opt-in (override `UseTransactionScope => true`)

## License

[MIT](License.md)
