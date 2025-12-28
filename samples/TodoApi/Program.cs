using System.Text.Json;
using AsyncMediator;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Redis
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379"));

// AsyncMediator with source generator + pipeline behavior
builder.Services.AddAsyncMediator(cfg => cfg
    .AddOpenGenericBehavior(typeof(LoggingBehavior<,>)));

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

// GET /api/todos - Load all todos (uses ILookupQuery)
app.MapGet("/api/todos", async (IMediator mediator, CancellationToken ct) =>
    Results.Ok(await mediator.LoadList<List<TodoItem>>(ct)));

// POST /api/todos - Create todo
app.MapPost("/api/todos", async (CreateTodoRequest req, IMediator mediator, CancellationToken ct) =>
{
    var result = await mediator.Send(new CreateTodoCommand(req.Title), ct);
    return result.Success
        ? Results.Created($"/api/todos/{result.Result<TodoItem>()?.Id}", result.Result<TodoItem>())
        : Results.BadRequest(result.ValidationResults.Select(v => v.ErrorMessage));
});

// PUT /api/todos/{id}/complete - Complete todo
app.MapPut("/api/todos/{id}/complete", async (string id, IMediator mediator, CancellationToken ct) =>
{
    var result = await mediator.Send(new CompleteTodoCommand(id), ct);
    return result.Success ? Results.NoContent() : Results.NotFound();
});

// DELETE /api/todos/{id} - Delete todo
app.MapDelete("/api/todos/{id}", async (string id, IMediator mediator, CancellationToken ct) =>
{
    var result = await mediator.Send(new DeleteTodoCommand(id), ct);
    return result.Success ? Results.NoContent() : Results.NotFound();
});

app.Run();

// === Models ===
public sealed record TodoItem(string Id, string Title, bool IsCompleted, DateTime CreatedAt);
public sealed record CreateTodoRequest(string Title);

// === Commands ===
public sealed record CreateTodoCommand(string Title) : ICommand;
public sealed record CompleteTodoCommand(string Id) : ICommand;
public sealed record DeleteTodoCommand(string Id) : ICommand;

// === Events ===
public sealed record TodoCreatedEvent(string Id, string Title, DateTime CreatedAt) : IDomainEvent;
public sealed record TodoCompletedEvent(string Id, DateTime CompletedAt) : IDomainEvent;

// === Query: Load all todos (ILookupQuery - no criteria needed) ===
public sealed class GetAllTodosQuery(IConnectionMultiplexer redis) : ILookupQuery<List<TodoItem>>
{
    public async Task<List<TodoItem>> Query(CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        var ids = await db.SetMembersAsync("todos:ids");
        var todos = new List<TodoItem>();

        foreach (var id in ids)
        {
            var json = await db.StringGetAsync($"todos:{id}");
            if (json.HasValue)
                todos.Add(JsonSerializer.Deserialize<TodoItem>((string)json!)!);
        }

        return todos.OrderByDescending(t => t.CreatedAt).ToList();
    }
}

// === Command Handlers (using CommandHandler base class) ===
public sealed class CreateTodoHandler(IMediator mediator, IConnectionMultiplexer redis)
    : CommandHandler<CreateTodoCommand>(mediator)
{
    protected override Task Validate(ValidationContext ctx, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(Command.Title))
            ctx.ValidationResults.Add(new System.ComponentModel.DataAnnotations.ValidationResult("Title is required"));
        return Task.CompletedTask;
    }

    protected override async Task<ICommandWorkflowResult> DoHandle(ValidationContext ctx, CancellationToken ct)
    {
        var db = redis.GetDatabase();
        var todo = new TodoItem(Guid.NewGuid().ToString(), Command.Title.Trim(), false, DateTime.UtcNow);

        await db.StringSetAsync($"todos:{todo.Id}", JsonSerializer.Serialize(todo));
        await db.SetAddAsync("todos:ids", todo.Id);

        // Defer event - will execute after handler succeeds
        Mediator.DeferEvent(new TodoCreatedEvent(todo.Id, todo.Title, todo.CreatedAt));

        var result = CommandWorkflowResult.Ok();
        result.SetResult(todo);
        return result;
    }
}

public sealed class CompleteTodoHandler(IMediator mediator, IConnectionMultiplexer redis)
    : CommandHandler<CompleteTodoCommand>(mediator)
{
    protected override Task Validate(ValidationContext ctx, CancellationToken ct) => Task.CompletedTask;

    protected override async Task<ICommandWorkflowResult> DoHandle(ValidationContext ctx, CancellationToken ct)
    {
        var db = redis.GetDatabase();
        var json = await db.StringGetAsync($"todos:{Command.Id}");

        if (!json.HasValue)
            return new CommandWorkflowResult("Todo not found");

        var todo = JsonSerializer.Deserialize<TodoItem>((string)json!)!;
        var updated = todo with { IsCompleted = true };
        await db.StringSetAsync($"todos:{Command.Id}", JsonSerializer.Serialize(updated));

        Mediator.DeferEvent(new TodoCompletedEvent(Command.Id, DateTime.UtcNow));
        return CommandWorkflowResult.Ok();
    }
}

public sealed class DeleteTodoHandler(IMediator mediator, IConnectionMultiplexer redis)
    : CommandHandler<DeleteTodoCommand>(mediator)
{
    protected override Task Validate(ValidationContext ctx, CancellationToken ct) => Task.CompletedTask;

    protected override async Task<ICommandWorkflowResult> DoHandle(ValidationContext ctx, CancellationToken ct)
    {
        var db = redis.GetDatabase();
        var existed = await db.KeyDeleteAsync($"todos:{Command.Id}");
        await db.SetRemoveAsync("todos:ids", Command.Id);

        return existed ? CommandWorkflowResult.Ok() : new CommandWorkflowResult("Todo not found");
    }
}

// === Event Handlers ===
public sealed class TodoCreatedEventHandler : IEventHandler<TodoCreatedEvent>
{
    public Task Handle(TodoCreatedEvent @event, CancellationToken ct = default)
    {
        Console.WriteLine($"[EVENT] Todo created: {@event.Title} (ID: {@event.Id}) at {@event.CreatedAt:u}");
        return Task.CompletedTask;
    }
}

public sealed class TodoCompletedEventHandler : IEventHandler<TodoCompletedEvent>
{
    public Task Handle(TodoCompletedEvent @event, CancellationToken ct = default)
    {
        Console.WriteLine($"[EVENT] Todo completed: ID {@event.Id} at {@event.CompletedAt:u}");
        return Task.CompletedTask;
    }
}

// === Pipeline Behavior (cross-cutting logging) ===
public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var requestName = typeof(TRequest).Name;
        Console.WriteLine($"[PIPELINE] Executing {requestName}");

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var response = await next();
        sw.Stop();

        Console.WriteLine($"[PIPELINE] Completed {requestName} in {sw.ElapsedMilliseconds}ms");
        return response;
    }
}
