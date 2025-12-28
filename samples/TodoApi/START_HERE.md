# AsyncMediator Quick Reference

## Quick Start

**Prerequisites:** .NET 10 SDK, Docker (or Redis on localhost:6379)

```bash
cd samples/TodoApi
docker-compose up -d                    # Start Redis
dotnet run --urls http://localhost:5050 # Run API
```

Open http://localhost:5050 and watch the console for `[PIPELINE]` and `[EVENT]` logs.

**Stop:**
```bash
docker-compose down      # Stop Redis
docker-compose down -v   # Stop + delete data
```

---

## Request Flow

```
Request → Pipeline Behaviors → Handler → Deferred Events
              ↓                   ↓            ↓
         [PIPELINE] log      Execute       [EVENT] log
```

1. **Request arrives** (Command or Query)
2. **Pipeline behaviors** intercept (logging, validation, etc.)
3. **Handler executes** business logic
4. **Events fire** only if handler succeeded

---

## Commands

Commands change state. They implement the marker interface `ICommand`.

```csharp
public sealed record CreateTodoCommand(string Title) : ICommand;
```

**Send a command:**
```csharp
var result = await mediator.Send(new CreateTodoCommand("Buy milk"), ct);
if (result.Success)
    Console.WriteLine("Created!");
```

---

## Command Handlers

Extend `CommandHandler<T>` for built-in validation and event execution.

```csharp
public sealed class CreateTodoHandler(IMediator mediator, IConnectionMultiplexer redis)
    : CommandHandler<CreateTodoCommand>(mediator)
{
    protected override Task Validate(ValidationContext ctx, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(Command.Title))
            ctx.ValidationResults.Add(new ValidationResult("Title required"));
        return Task.CompletedTask;
    }

    protected override async Task<ICommandWorkflowResult> DoHandle(ValidationContext ctx, CancellationToken ct)
    {
        // Business logic here
        Mediator.DeferEvent(new TodoCreatedEvent(id, Command.Title));
        return CommandWorkflowResult.Ok();
    }
}
```

**Flow:** `Validate()` → if valid → `DoHandle()` → if success → execute deferred events

### Returning Data from Commands

Use `SetResult()` to return data (e.g., created entity):

```csharp
protected override async Task<ICommandWorkflowResult> DoHandle(ValidationContext ctx, CancellationToken ct)
{
    var todo = new TodoItem(Guid.NewGuid().ToString(), Command.Title);
    await db.SaveAsync(todo, ct);

    var result = CommandWorkflowResult.Ok();
    result.SetResult(todo);  // Attach result
    return result;
}

// Caller retrieves it:
var result = await mediator.Send(command, ct);
var todo = result.Result<TodoItem>();  // null if failed or wrong type
```

---

## Queries

Queries read data without side effects.

**No criteria (load all):**
```csharp
public sealed class GetAllTodosQuery(IConnectionMultiplexer redis) : ILookupQuery<List<TodoItem>>
{
    public async Task<List<TodoItem>> Query(CancellationToken ct = default)
    {
        // Load and return data
    }
}
```

```csharp
var todos = await mediator.LoadList<List<TodoItem>>(ct);
```

**With criteria:**
```csharp
public sealed class GetTodoByIdQuery(IConnectionMultiplexer redis) : IQuery<string, TodoItem?>
{
    public async Task<TodoItem?> Query(string id, CancellationToken ct = default)
    {
        // Load by ID
    }
}
```

```csharp
var todo = await mediator.Query<string, TodoItem?>(todoId, ct);
```

---

## Domain Events

Events notify other parts of the system after something happened.

```csharp
public sealed record TodoCreatedEvent(string Id, string Title) : IDomainEvent;
```

**Defer an event** (fires after handler succeeds):
```csharp
Mediator.DeferEvent(new TodoCreatedEvent(todo.Id, todo.Title));
```

**Handle an event:**
```csharp
public sealed class TodoCreatedEventHandler : IEventHandler<TodoCreatedEvent>
{
    public Task Handle(TodoCreatedEvent @event, CancellationToken ct = default)
    {
        Console.WriteLine($"[EVENT] Created: {@event.Title}");
        return Task.CompletedTask;
    }
}
```

Multiple handlers per event are supported - all execute in sequence.

---

## Pipeline Behaviors

Behaviors wrap all requests for cross-cutting concerns.

```csharp
public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        Console.WriteLine($"[PIPELINE] {typeof(TRequest).Name}");
        var response = await next();  // Call next in chain
        Console.WriteLine($"[PIPELINE] Done");
        return response;
    }
}
```

Use cases: logging, timing, validation, caching, authorization, exception handling.

---

## Registration

```csharp
builder.Services.AddAsyncMediator(cfg => cfg
    .AddOpenGenericBehavior(typeof(LoggingBehavior<,>)));
```

- **Handlers**: Auto-discovered at compile time by source generator
- **Behaviors**: Register with `AddOpenGenericBehavior()`
- No `RegisterServicesFromAssembly` needed

---

## Key Behaviors

| Behavior | When Events Fire | On Exception |
|----------|------------------|--------------|
| Success path | After `DoHandle` returns `Success=true` | Events cleared |
| Validation fails | Never | Events cleared |
| Exception thrown | Never | Events cleared |

**Safe by default** - events only execute when the operation succeeds.

---

## API Summary

| Concept | Interface | Call Method |
|---------|-----------|-------------|
| Command | `ICommand` | `mediator.Send(cmd)` |
| Command Handler | `CommandHandler<T>` | Auto-resolved |
| Query (no criteria) | `ILookupQuery<TResult>` | `mediator.LoadList<T>()` |
| Query (with criteria) | `IQuery<TCriteria, TResult>` | `mediator.Query<C,R>(c)` |
| Event | `IDomainEvent` | `mediator.DeferEvent(e)` |
| Event Handler | `IEventHandler<T>` | Auto-resolved |
| Behavior | `IPipelineBehavior<TReq, TRes>` | `AddOpenGenericBehavior()` |

---

## Console Output

When working correctly, you'll see:
```
[PIPELINE] Executing CreateTodoCommand
[EVENT] Todo created: Buy milk (ID: abc-123)
[PIPELINE] Completed CreateTodoCommand in 12ms
```
