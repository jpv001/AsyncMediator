# AsyncMediator

A lightweight, high-performance mediator for .NET 9/10. Zero dependencies. Minimal allocations.

```
Install-Package AsyncMediator
```

## Quick Start

### 1. Wire Up DI

```csharp
services.AddScoped<IMediator>(sp => new Mediator(
    type => sp.GetServices(type),
    type => sp.GetRequiredService(type)));

services.AddTransient<ICommandHandler<CreateOrderCommand>, CreateOrderHandler>();
services.AddTransient<IEventHandler<OrderCreatedEvent>, SendConfirmationEmailHandler>();
services.AddTransient<IQuery<OrderSearchCriteria, List<Order>>, OrderQuery>();
```

### 2. Define a Command

```csharp
public record CreateOrderCommand(Guid CustomerId, List<OrderItem> Items) : ICommand;

public class CreateOrderHandler(IMediator mediator, IOrderRepository repo)
    : CommandHandler<CreateOrderCommand>(mediator)
{
    protected override Task Validate(ValidationContext ctx, CancellationToken cancellationToken)
    {
        if (Command.Items.Count == 0)
            ctx.AddError(nameof(Command.Items), "Order must have items");
        return Task.CompletedTask;
    }

    protected override async Task<ICommandWorkflowResult> DoHandle(ValidationContext ctx, CancellationToken cancellationToken)
    {
        var order = await repo.Create(Command.CustomerId, Command.Items, cancellationToken);
        Mediator.DeferEvent(new OrderCreatedEvent(order.Id));
        return CommandWorkflowResult.Ok();
    }
}
```

### 3. Send It

```csharp
var result = await mediator.Send(new CreateOrderCommand(customerId, items));
if (!result.Success)
    return BadRequest(result.ValidationResults);

// With cancellation support (e.g., from HttpContext.RequestAborted)
var result = await mediator.Send(command, cancellationToken);
```

## Queries

```csharp
// With criteria
public record OrderSearchCriteria(Guid? CustomerId, DateOnly? Since);

public class OrderQuery(IOrderRepository repo) : IQuery<OrderSearchCriteria, List<Order>>
{
    public Task<List<Order>> Query(OrderSearchCriteria c, CancellationToken cancellationToken = default) =>
        repo.Search(c.CustomerId, c.Since, cancellationToken);
}

var orders = await mediator.Query<OrderSearchCriteria, List<Order>>(criteria);

// Without criteria
public class CountryLookup(ICountryRepository repo) : ILookupQuery<List<Country>>
{
    public Task<List<Country>> Query(CancellationToken cancellationToken = default) =>
        repo.GetAll(cancellationToken);
}

var countries = await mediator.LoadList<List<Country>>();
```

## Events

Events are deferred during command execution and published after `DoHandle` completes.

```csharp
public record OrderCreatedEvent(Guid OrderId) : IDomainEvent;

public class SendConfirmationEmailHandler(IEmailService email) : IEventHandler<OrderCreatedEvent>
{
    public async Task Handle(OrderCreatedEvent e, CancellationToken cancellationToken = default) =>
        await email.SendOrderConfirmation(e.OrderId, cancellationToken);
}
```

Multiple handlers per event are supported. They execute in DI registration order.

## Pipeline Behaviors (Cross-Cutting Concerns)

Pipeline behaviors wrap handler execution for cross-cutting concerns. No overhead when no behaviors are registered.

### Option 1: DI Factory (Recommended)

Use `behaviorFactory` to resolve open generic behaviors from your DI container. This applies behaviors to ALL commands automatically:

```csharp
// Register open generic behaviors
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

// Use behaviorFactory to resolve behaviors from DI
services.AddScoped<IMediator>(sp => new Mediator(
    type => sp.GetServices(type),
    type => sp.GetRequiredService(type),
    behaviorFactory: type => sp.GetServices(type)));
```

### Option 2: Explicit Registration

For specific behaviors on specific commands:

```csharp
services.AddScoped<IMediator>(sp => new Mediator(
    type => sp.GetServices(type),
    type => sp.GetRequiredService(type),
    behaviors: [
        new LoggingBehavior<CreateOrderCommand, ICommandWorkflowResult>(),
        new MetricsBehavior<CreateOrderCommand, ICommandWorkflowResult>()
    ]));
```

### Example: Logging Behavior

```csharp
public class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {RequestType}", typeof(TRequest).Name);
        var sw = Stopwatch.StartNew();
        var response = await next();
        logger.LogInformation("Handled {RequestType} in {ElapsedMs}ms", typeof(TRequest).Name, sw.ElapsedMilliseconds);
        return response;
    }
}
```

### Example: Validation with DataAnnotations

Use built-in `System.ComponentModel.DataAnnotations`:

```csharp
public record CreateOrderCommand(
    [property: Required, MinLength(1)] Guid CustomerId,
    [property: Required, MinLength(1)] List<OrderItem> Items) : ICommand;

public class ValidationBehavior<TRequest> : IPipelineBehavior<TRequest, ICommandWorkflowResult>
    where TRequest : ICommand
{
    public Task<ICommandWorkflowResult> Handle(
        TRequest request,
        RequestHandlerDelegate<ICommandWorkflowResult> next,
        CancellationToken cancellationToken)
    {
        var context = new System.ComponentModel.DataAnnotations.ValidationContext(request);
        var results = new List<ValidationResult>();

        if (!Validator.TryValidateObject(request, context, results, validateAllProperties: true))
        {
            return Task.FromResult<ICommandWorkflowResult>(
                new CommandWorkflowResult(results));
        }

        return next();
    }
}
```

Behaviors execute in registration order, wrapping around the handler like middleware. Each behavior can:
- Execute logic before/after the handler
- Short-circuit by not calling `next()`
- Catch and handle exceptions

## Transactions

TransactionScope is **opt-in** for performance. Override when ACID guarantees are needed:

```csharp
public class TransferFundsHandler(IMediator mediator) : CommandHandler<TransferFundsCommand>(mediator)
{
    protected override bool UseTransactionScope => true;  // Enables TransactionScope

    protected override async Task<ICommandWorkflowResult> DoHandle(ValidationContext ctx, CancellationToken cancellationToken)
    {
        await DebitAccount(Command.FromAccount, Command.Amount, cancellationToken);
        await CreditAccount(Command.ToAccount, Command.Amount, cancellationToken);
        Mediator.DeferEvent(new FundsTransferredEvent(Command.FromAccount, Command.ToAccount));
        return CommandWorkflowResult.Ok();
    }
}
```

## Performance

| Operation | Latency | Memory |
|-----------|---------|--------|
| Command (success) | ~170 ns | ~200 B |
| Command (10 concurrent) | ~1.7 μs | ~2 KB |
| Query | ~150 ns | ~200 B |

87% faster and 83% less memory than v2.x.

## Docs

- [Architecture & Design Decisions](ARCHITECTURE.md)
- [Pipeline Behaviors Guide](PIPELINE.md)
- [Migration Guide (v2 → v3)](MIGRATION_GUIDE.md)

## Cancellation Support

All async operations support cancellation tokens for graceful shutdown and timeout handling:

```csharp
// In ASP.NET Core controllers
public async Task<IActionResult> CreateOrder(CreateOrderCommand command, CancellationToken cancellationToken)
{
    var result = await _mediator.Send(command, cancellationToken);
    return result.Success ? Ok() : BadRequest(result.ValidationResults);
}

// With timeout
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
var result = await mediator.Send(command, cts.Token);

// Execute deferred events with cancellation
await mediator.ExecuteDeferredEvents(cancellationToken);
```

## Breaking Changes (v3.0)

- Requires .NET 9 or .NET 10
- `CancellationToken` parameter added to all async interfaces (see [Migration Guide](MIGRATION_GUIDE.md))
- `TransactionScope` now opt-in (override `UseTransactionScope => true`)
- Removed `HandlerOrderAttribute` (use DI registration order)
- `ICommandWorkflowResult.ValidationResults` is now `List<T>`

## License

[MIT](License.md)
