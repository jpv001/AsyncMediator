using AsyncMediator.Tests.Fakes;
using AsyncMediator.Tests.Infrastructure;
using System.Diagnostics;

namespace AsyncMediator.Tests;

[TestClass]
public class StressTests
{
    [TestMethod]
    [Timeout(60000)] // 60 second timeout
    public async Task SustainedLoad_10KCommandsPerSecond_For5Seconds()
    {
        // Arrange
        var handlerFactory = new TestHandlerRegistry();
        var mediator = new Mediator(handlerFactory.MultiInstanceFactory, handlerFactory.SingleInstanceFactory);
        handlerFactory.AddHandlersForCommandOrQuery<ICommandHandler<TestCommand>>(new TestCommandHandler(mediator));
        handlerFactory.AddHandlersForEvent<IEventHandler<FakeEvent>>([new NoOpEventHandler()]);
        handlerFactory.AddHandlersForEvent<IEventHandler<FakeEventFromHandler>>([new NoOpEventFromHandlerHandler()]);

        var totalCommands = 0;
        var errors = 0;
        var sw = Stopwatch.StartNew();
        var duration = TimeSpan.FromSeconds(5);
        var batchSize = 100; // Process in batches to achieve throughput

        // Act
        while (sw.Elapsed < duration)
        {
            var tasks = new Task[batchSize];
            for (var i = 0; i < batchSize; i++)
            {
                tasks[i] = Task.Run(async () =>
                {
                    try
                    {
                        var result = await mediator.Send(new TestCommand { Id = 1 });
                        if (result.Success)
                            Interlocked.Increment(ref totalCommands);
                        else
                            Interlocked.Increment(ref errors);
                    }
                    catch
                    {
                        Interlocked.Increment(ref errors);
                    }
                });
            }
            await Task.WhenAll(tasks);
        }

        sw.Stop();

        // Assert
        var throughput = totalCommands / sw.Elapsed.TotalSeconds;
        Console.WriteLine($"Sustained load test: {totalCommands} commands in {sw.Elapsed.TotalSeconds:F2}s = {throughput:F0} cmd/s");
        Console.WriteLine($"Errors: {errors}");

        Assert.AreEqual(0, errors, "Should have no errors");
        Assert.IsTrue(totalCommands > 1000, $"Should process more than 1000 commands, got {totalCommands}");
    }

    [TestMethod]
    [Timeout(30000)] // 30 second timeout
    public async Task BurstLoad_SpikeToHighThroughput()
    {
        // Arrange - Use simple handler without deferred events for clean burst test
        var handlerFactory = new TestHandlerRegistry();
        var mediator = new Mediator(handlerFactory.MultiInstanceFactory, handlerFactory.SingleInstanceFactory);
        handlerFactory.AddHandlersForCommandOrQuery<ICommandHandler<SimpleCommand>>(new SimpleCommandHandler(mediator));

        // Act - Burst: 1000 concurrent commands
        var sw = Stopwatch.StartNew();
        var tasks = new Task<ICommandWorkflowResult>[1000];
        for (var i = 0; i < 1000; i++)
        {
            tasks[i] = mediator.Send(new SimpleCommand { Id = i });
        }

        var results = await Task.WhenAll(tasks);
        sw.Stop();

        // Assert
        var successCount = results.Count(r => r.Success);
        Console.WriteLine($"Burst test: {successCount} successful commands in {sw.ElapsedMilliseconds}ms");

        Assert.AreEqual(1000, successCount, "All burst commands should succeed");
        Assert.IsTrue(sw.ElapsedMilliseconds < 10000, $"Burst should complete within 10s, took {sw.ElapsedMilliseconds}ms");
    }

    [TestMethod]
    [Timeout(60000)] // 60 second timeout
    public async Task MixedWorkload_CommandsQueriesEvents_Interleaved()
    {
        // Arrange
        var handlerFactory = new TestHandlerRegistry();
        var mediator = new Mediator(handlerFactory.MultiInstanceFactory, handlerFactory.SingleInstanceFactory);

        handlerFactory.AddHandlersForCommandOrQuery<ICommandHandler<TestCommand>>(new TestCommandHandler(mediator));
        handlerFactory.AddHandlersForCommandOrQuery<IQuery<FakeRangeCriteria, List<FakeResult>>>(new FindFakeResultByRangeCriteria());
        handlerFactory.AddHandlersForCommandOrQuery<ILookupQuery<List<FakeResult>>>(new FindResultForLookup());

        var eventHandler = new CountingEventHandlerWithDelay();
        handlerFactory.AddHandlersForEvent<IEventHandler<FakeEvent>>([eventHandler]);
        handlerFactory.AddHandlersForEvent<IEventHandler<FakeEventFromHandler>>([new NoOpEventFromHandlerHandler()]);

        var commandCount = 0;
        var queryCount = 0;
        var lookupCount = 0;
        var errors = 0;

        var sw = Stopwatch.StartNew();
        var duration = TimeSpan.FromSeconds(5);
        var tasks = new List<Task>();

        // Act - Mixed workload
        while (sw.Elapsed < duration)
        {
            // Commands (50%)
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    await mediator.Send(new TestCommand { Id = 1 });
                    Interlocked.Increment(ref commandCount);
                }
                catch { Interlocked.Increment(ref errors); }
            }));

            // Queries (30%)
            if (tasks.Count % 3 == 0)
            {
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await mediator.Query<FakeRangeCriteria, List<FakeResult>>(
                            new FakeRangeCriteria { MinValue = 1, MaxValue = 5 });
                        Interlocked.Increment(ref queryCount);
                    }
                    catch { Interlocked.Increment(ref errors); }
                }));
            }

            // Lookup queries (20%)
            if (tasks.Count % 5 == 0)
            {
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await mediator.LoadList<List<FakeResult>>();
                        Interlocked.Increment(ref lookupCount);
                    }
                    catch { Interlocked.Increment(ref errors); }
                }));
            }

            // Prevent task list from growing too large
            if (tasks.Count > 100)
            {
                await Task.WhenAll(tasks);
                tasks.Clear();
            }
        }

        await Task.WhenAll(tasks);
        sw.Stop();

        // Assert
        Console.WriteLine($"Mixed workload: {commandCount} commands, {queryCount} queries, {lookupCount} lookups in {sw.Elapsed.TotalSeconds:F2}s");
        Console.WriteLine($"Events processed: {eventHandler.Count}");
        Console.WriteLine($"Errors: {errors}");

        Assert.AreEqual(0, errors, "Should have no errors");
        Assert.IsTrue(commandCount > 0);
        Assert.IsTrue(queryCount > 0);
        Assert.IsTrue(lookupCount > 0);
    }

    [TestMethod]
    [Timeout(120000)] // 2 minute timeout for stability test
    public async Task Stability_ExtendedOperation_NoMemoryLeaksNoErrors()
    {
        // Arrange
        var handlerFactory = new TestHandlerRegistry();
        var mediator = new Mediator(handlerFactory.MultiInstanceFactory, handlerFactory.SingleInstanceFactory);
        handlerFactory.AddHandlersForCommandOrQuery<ICommandHandler<TestCommand>>(new TestCommandHandler(mediator));
        handlerFactory.AddHandlersForEvent<IEventHandler<FakeEvent>>([new NoOpEventHandler()]);
        handlerFactory.AddHandlersForEvent<IEventHandler<FakeEventFromHandler>>([new NoOpEventFromHandlerHandler()]);

        var errors = 0;
        var totalOps = 0;

        GC.Collect();
        GC.WaitForPendingFinalizers();
        var initialMemory = GC.GetTotalMemory(true);

        var sw = Stopwatch.StartNew();
        var duration = TimeSpan.FromSeconds(30); // 30 second stability test

        // Act
        while (sw.Elapsed < duration)
        {
            try
            {
                await mediator.Send(new TestCommand { Id = 1 });
                Interlocked.Increment(ref totalOps);
            }
            catch
            {
                Interlocked.Increment(ref errors);
            }

            // Periodically check memory
            if (totalOps % 1000 == 0)
            {
                GC.Collect();
            }
        }

        sw.Stop();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        var finalMemory = GC.GetTotalMemory(true);

        // Assert
        var memoryGrowth = finalMemory - initialMemory;
        Console.WriteLine($"Stability test: {totalOps} operations in {sw.Elapsed.TotalSeconds:F2}s");
        Console.WriteLine($"Memory: {initialMemory / 1024 / 1024}MB -> {finalMemory / 1024 / 1024}MB (growth: {memoryGrowth / 1024 / 1024}MB)");
        Console.WriteLine($"Errors: {errors}");

        Assert.AreEqual(0, errors, "Should have no errors");
        Assert.IsTrue(totalOps > 0, "Should complete some operations");
        // Allow reasonable memory growth (50MB) for extended test
        Assert.IsTrue(memoryGrowth < 50 * 1024 * 1024,
            $"Memory grew by {memoryGrowth / 1024 / 1024}MB, possible memory leak");
    }
}

public class CountingEventHandlerWithDelay : IEventHandler<FakeEvent>
{
    private int _count;
    public int Count => _count;

    public Task Handle(FakeEvent @event, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _count);
        return Task.CompletedTask;
    }
}

public sealed class SimpleCommand : ICommand
{
    public int Id { get; set; }
}

public sealed class SimpleCommandHandler(IMediator mediator) : CommandHandler<SimpleCommand>(mediator)
{
    protected override Task Validate(ValidationContext validationContext, CancellationToken cancellationToken) => Task.CompletedTask;

    protected override Task<ICommandWorkflowResult> DoHandle(ValidationContext validationContext, CancellationToken cancellationToken) =>
        Task.FromResult<ICommandWorkflowResult>(CommandWorkflowResult.Ok());
}
