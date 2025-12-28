# AsyncMediator

[![NuGet](https://img.shields.io/nuget/v/AsyncMediator.svg)](https://www.nuget.org/packages/AsyncMediator)
[![NuGet](https://img.shields.io/nuget/v/AsyncMediator.SourceGenerator.svg?label=SourceGenerator)](https://www.nuget.org/packages/AsyncMediator.SourceGenerator)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A lightweight, high-performance mediator for .NET 9/10 with compile-time handler discovery.

## Highlights

- **Zero-config setup** - Source generator discovers handlers at compile time
- **Zero runtime dependencies** - Core library has no external packages
- **Safe event deferral** - Events only fire after successful command execution
- **Pipeline behaviors** - Add logging, validation, caching without touching handlers
- **Built-in validation** - `CommandHandler<T>` base class with `Validate()` + `DoHandle()` flow
- **High performance** - ~163ns command dispatch, minimal allocations

## Installation

```bash
dotnet add package AsyncMediator
dotnet add package AsyncMediator.SourceGenerator
```

> **Recommended:** Always install both packages. The source generator eliminates manual handler registration and catches missing handlers at compile time.

## Quick Start

### 1. Register in Program.cs

```csharp
builder.Services.AddAsyncMediator();
```

That's it. All handlers are discovered automatically.

### 2. Create a Command

```csharp
public record CreateOrderCommand(Guid CustomerId, List<OrderItem> Items) : ICommand;
```

### 3. Create a Handler

```csharp
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
        Mediator.DeferEvent(new OrderCreatedEvent(order.Id));
        return CommandWorkflowResult.Ok();
    }
}
```

### 4. Send Commands

```csharp
var result = await mediator.Send(new CreateOrderCommand(customerId, items), ct);
if (result.Success)
    // Order created, events fired
```

## Core Concepts

### Commands

Commands change state. Handlers return `ICommandWorkflowResult` with validation support.

```csharp
public record CreateOrderCommand(Guid CustomerId) : ICommand;
```

### Queries

Queries read data without side effects.

```csharp
// With criteria
public class OrderQuery(IOrderRepository repo) : IQuery<OrderSearchCriteria, List<Order>>
{
    public Task<List<Order>> Query(OrderSearchCriteria c, CancellationToken ct) =>
        repo.Search(c.CustomerId, ct);
}

var orders = await mediator.Query<OrderSearchCriteria, List<Order>>(criteria, ct);

// Without criteria
public class AllOrdersQuery(IOrderRepository repo) : ILookupQuery<List<Order>>
{
    public Task<List<Order>> Query(CancellationToken ct) => repo.GetAll(ct);
}

var orders = await mediator.LoadList<List<Order>>(ct);
```

### Events

Events fire after successful command execution. They're automatically skipped if validation fails or an exception occurs.

```csharp
public record OrderCreatedEvent(Guid OrderId) : IDomainEvent;

// Defer in handler
Mediator.DeferEvent(new OrderCreatedEvent(order.Id));

// Handle elsewhere
public class SendEmailHandler : IEventHandler<OrderCreatedEvent>
{
    public Task Handle(OrderCreatedEvent e, CancellationToken ct) =>
        emailService.SendConfirmation(e.OrderId, ct);
}
```

### Pipeline Behaviors

Add cross-cutting concerns without modifying handlers:

```csharp
builder.Services.AddAsyncMediator(cfg => cfg
    .AddOpenGenericBehavior(typeof(LoggingBehavior<,>))
    .AddOpenGenericBehavior(typeof(ValidationBehavior<,>)));
```

```csharp
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        Console.WriteLine($"Handling {typeof(TRequest).Name}");
        var response = await next();
        Console.WriteLine($"Handled {typeof(TRequest).Name}");
        return response;
    }
}
```

## Performance

| Operation | Latency | Memory |
|-----------|---------|--------|
| Send command | ~163 ns | ~488 B |
| Query | ~105 ns | ~248 B |
| Defer event | ~575 ns | 0 B |

Pipeline behaviors add zero overhead when not registered.

## When to Use

**Good fit:**
- CQRS architectures
- Domain-driven design with domain events
- Clean architecture / vertical slices
- Applications requiring testability

**Not a fit:**
- Simple CRUD (direct repository access is clearer)
- Event sourcing (use specialized frameworks)
- Real-time streaming (use message brokers)

## Documentation

| Resource | Description |
|----------|-------------|
| [Working Demo](samples/TodoApi/START_HERE.md) | Run it locally and see the flow |
| [Architecture](ARCHITECTURE.md) | Design decisions and internals |
| [Pipeline Behaviors](PIPELINE.md) | Logging, validation, unit of work examples |
| [Migration Guide](MIGRATION_GUIDE.md) | Upgrading from v2.x |

## Advanced Topics

<details>
<summary><strong>Returning data from commands</strong></summary>

```csharp
var result = CommandWorkflowResult.Ok();
result.SetResult(order);
return result;

// Caller
var order = result.Result<Order>();
```
</details>

<details>
<summary><strong>TransactionScope (opt-in)</strong></summary>

```csharp
public class TransferHandler(IMediator mediator) : CommandHandler<TransferCommand>(mediator)
{
    protected override bool UseTransactionScope => true;
}
```
</details>

<details>
<summary><strong>Excluding handlers from discovery</strong></summary>

```csharp
[ExcludeFromMediator]
public class DraftHandler : CommandHandler<MyCommand> { ... }
```
</details>

<details>
<summary><strong>Manual registration (without source generator)</strong></summary>

```csharp
services.AddScoped<IMediator>(sp => new Mediator(
    type => sp.GetServices(type),
    type => sp.GetRequiredService(type)));

services.AddTransient<ICommandHandler<CreateOrderCommand>, CreateOrderHandler>();
```
</details>

## Contributing

Found a bug or have a feature request? [Open an issue](https://github.com/preemajames/AsyncMediator/issues).

## License

[MIT](License.md)
