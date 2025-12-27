using AsyncMediator.Tests.Fakes;
using AsyncMediator.Tests.Infrastructure;
using NSubstitute;

namespace AsyncMediator.Tests;

[TestClass]
public class ConcurrencyTests
{
    [TestMethod]
    public async Task ConcurrentCommandSends_100Threads_ShouldProcessAllWithoutLoss()
    {
        // Arrange
        var handlerFactory = new TestHandlerRegistry();
        var mediator = new Mediator(handlerFactory.MultiInstanceFactory, handlerFactory.SingleInstanceFactory);
        handlerFactory.AddHandlersForCommandOrQuery<ICommandHandler<TestCommand>>(new TestCommandHandler(mediator));

        var successCount = 0;
        var tasks = new Task[100];

        // Act - Send 100 concurrent commands
        for (var i = 0; i < 100; i++)
        {
            tasks[i] = Task.Run(async () =>
            {
                var result = await mediator.Send(new TestCommand { Id = 1 });
                if (result.Success)
                    Interlocked.Increment(ref successCount);
            });
        }

        await Task.WhenAll(tasks);

        // Assert
        Assert.AreEqual(100, successCount, "All concurrent commands should succeed");
    }

    [TestMethod]
    public async Task EventDeferral_MultipleThreads_50Threads_NoEventLoss()
    {
        // Arrange
        var handlerFactory = new TestHandlerRegistry();
        var mediator = new Mediator(handlerFactory.MultiInstanceFactory, handlerFactory.SingleInstanceFactory);

        var processedCount = 0;
        var handler = Substitute.For<HandlerWithoutAdditionalEvents>();
        handler.Handle(Arg.Any<FakeEvent>()).Returns(ci =>
        {
            Interlocked.Increment(ref processedCount);
            return Task.CompletedTask;
        });
        handlerFactory.AddHandlersForEvent<IEventHandler<FakeEvent>>([handler]);

        const int threadsCount = 50;
        const int eventsPerThread = 2000;
        var tasks = new Task[threadsCount];

        // Act - Defer events from 50 threads
        for (var i = 0; i < threadsCount; i++)
        {
            var threadId = i;
            tasks[i] = Task.Run(() =>
            {
                for (var j = 0; j < eventsPerThread; j++)
                {
                    mediator.DeferEvent(new FakeEvent { Id = (threadId * eventsPerThread) + j });
                }
            });
        }

        await Task.WhenAll(tasks);
        await mediator.ExecuteDeferredEvents();

        // Assert
        Assert.AreEqual(threadsCount * eventsPerThread, processedCount,
            $"Expected {threadsCount * eventsPerThread} events to be processed");
    }

    [TestMethod]
    public async Task ConcurrentEventDeferralAndExecution_ShouldNotDeadlock()
    {
        // Arrange
        var handlerFactory = new TestHandlerRegistry();
        var mediator = new Mediator(handlerFactory.MultiInstanceFactory, handlerFactory.SingleInstanceFactory);

        var processedCount = 0;
        var handler = Substitute.For<HandlerWithoutAdditionalEvents>();
        handler.Handle(Arg.Any<FakeEvent>()).Returns(ci =>
        {
            Interlocked.Increment(ref processedCount);
            return Task.CompletedTask;
        });
        handlerFactory.AddHandlersForEvent<IEventHandler<FakeEvent>>([handler]);

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var deferralTasks = new List<Task>();
        var executionTasks = new List<Task>();

        // Act - Concurrently defer and execute events
        for (var i = 0; i < 10; i++)
        {
            deferralTasks.Add(Task.Run(async () =>
            {
                for (var j = 0; j < 1000 && !cts.IsCancellationRequested; j++)
                {
                    mediator.DeferEvent(new FakeEvent { Id = j });
                    await Task.Yield();
                }
            }, cts.Token));

            executionTasks.Add(Task.Run(async () =>
            {
                while (!cts.IsCancellationRequested)
                {
                    await mediator.ExecuteDeferredEvents();
                    await Task.Delay(1);
                }
            }, cts.Token));
        }

        await Task.WhenAll(deferralTasks);
        cts.Cancel();

        try { await Task.WhenAll(executionTasks); }
        catch (OperationCanceledException) { }

        // Final execution to process remaining events
        await mediator.ExecuteDeferredEvents();

        // Assert - Should complete without deadlock (timeout would fail the test)
        Assert.IsTrue(processedCount > 0, "Some events should have been processed");
    }

    [TestMethod]
    public async Task MixedOperations_CommandsQueriesEvents_ConcurrentExecution()
    {
        // Arrange
        var handlerFactory = new TestHandlerRegistry();
        var mediator = new Mediator(handlerFactory.MultiInstanceFactory, handlerFactory.SingleInstanceFactory);

        handlerFactory.AddHandlersForCommandOrQuery<ICommandHandler<TestCommand>>(new TestCommandHandler(mediator));
        handlerFactory.AddHandlersForCommandOrQuery<IQuery<FakeRangeCriteria, List<FakeResult>>>(new FindFakeResultByRangeCriteria());

        var handler = Substitute.For<HandlerWithoutAdditionalEvents>();
        handlerFactory.AddHandlersForEvent<IEventHandler<FakeEvent>>([handler]);
        handlerFactory.AddHandlersForEvent<IEventHandler<FakeEventFromHandler>>([Substitute.For<DependentEventHandler>()]);

        var commandCount = 0;
        var queryCount = 0;
        var tasks = new List<Task>();

        // Act - Mix of commands, queries, and events
        for (var i = 0; i < 50; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                await mediator.Send(new TestCommand { Id = 1 });
                Interlocked.Increment(ref commandCount);
            }));

            tasks.Add(Task.Run(async () =>
            {
                await mediator.Query<FakeRangeCriteria, List<FakeResult>>(new FakeRangeCriteria { MinValue = 1, MaxValue = 5 });
                Interlocked.Increment(ref queryCount);
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        Assert.AreEqual(50, commandCount);
        Assert.AreEqual(50, queryCount);
    }

    [TestMethod]
    public async Task RaceCondition_CommandResult_ShouldBeThreadSafe()
    {
        // Arrange
        var handlerFactory = new TestHandlerRegistry();
        var mediator = new Mediator(handlerFactory.MultiInstanceFactory, handlerFactory.SingleInstanceFactory);
        handlerFactory.AddHandlersForCommandOrQuery<ICommandHandler<TestCommandWithResult>>(new TestCommandWithResultHandler(mediator));
        handlerFactory.AddHandlersForEvent<IEventHandler<FakeEvent>>([Substitute.For<HandlerWithoutAdditionalEvents>()]);
        handlerFactory.AddHandlersForEvent<IEventHandler<FakeEventFromHandler>>([Substitute.For<DependentEventHandler>()]);

        var results = new System.Collections.Concurrent.ConcurrentBag<int>();
        var tasks = new Task[100];

        // Act - Concurrent commands with results
        for (var i = 0; i < 100; i++)
        {
            tasks[i] = Task.Run(async () =>
            {
                var result = await mediator.Send(new TestCommandWithResult { Id = 1 });
                var commandResult = result.Result<TestCommandResult>();
                if (commandResult is not null)
                    results.Add(commandResult.ResultingValue);
            });
        }

        await Task.WhenAll(tasks);

        // Assert - All results should be consistent (value 5 from handler)
        Assert.AreEqual(100, results.Count);
        Assert.IsTrue(results.All(r => r == 5), "All results should have value 5");
    }

    [TestMethod]
    public async Task HighContention_FactoryResolution_ShouldBeThreadSafe()
    {
        // Arrange
        var handlerFactory = new TestHandlerRegistry();
        var mediator = new Mediator(handlerFactory.MultiInstanceFactory, handlerFactory.SingleInstanceFactory);

        handlerFactory.AddHandlersForCommandOrQuery<ICommandHandler<TestCommand>>(new TestCommandHandler(mediator));
        handlerFactory.AddHandlersForCommandOrQuery<ICommandHandler<TestCommandWithResult>>(new TestCommandWithResultHandler(mediator));
        handlerFactory.AddHandlersForCommandOrQuery<IQuery<FakeRangeCriteria, List<FakeResult>>>(new FindFakeResultByRangeCriteria());
        handlerFactory.AddHandlersForEvent<IEventHandler<FakeEvent>>([Substitute.For<HandlerWithoutAdditionalEvents>()]);
        handlerFactory.AddHandlersForEvent<IEventHandler<FakeEventFromHandler>>([Substitute.For<DependentEventHandler>()]);

        var errors = new System.Collections.Concurrent.ConcurrentBag<Exception>();
        var tasks = new Task[200];

        // Act - High contention on factory resolution
        for (var i = 0; i < 200; i++)
        {
            var index = i;
            tasks[i] = Task.Run(async () =>
            {
                try
                {
                    if (index % 3 == 0)
                        await mediator.Send(new TestCommand { Id = 1 });
                    else if (index % 3 == 1)
                        await mediator.Send(new TestCommandWithResult { Id = 1 });
                    else
                        await mediator.Query<FakeRangeCriteria, List<FakeResult>>(new FakeRangeCriteria { MinValue = 1, MaxValue = 5 });
                }
                catch (Exception ex)
                {
                    errors.Add(ex);
                }
            });
        }

        await Task.WhenAll(tasks);

        // Assert
        Assert.AreEqual(0, errors.Count, $"Errors occurred: {string.Join(", ", errors.Select(e => e.Message))}");
    }

    [TestMethod]
    public async Task ParallelEventDeferral_DuringCommandExecution_ShouldNotCorrupt()
    {
        // Arrange
        var handlerFactory = new TestHandlerRegistry();
        var mediator = new Mediator(handlerFactory.MultiInstanceFactory, handlerFactory.SingleInstanceFactory);

        handlerFactory.AddHandlersForCommandOrQuery<ICommandHandler<TestCommand>>(new TestCommandHandler(mediator));

        var eventHandler = Substitute.For<HandlerWithoutAdditionalEvents>();
        var eventFromHandlerHandler = Substitute.For<DependentEventHandler>();
        handlerFactory.AddHandlersForEvent<IEventHandler<FakeEvent>>([eventHandler]);
        handlerFactory.AddHandlersForEvent<IEventHandler<FakeEventFromHandler>>([eventFromHandlerHandler]);

        var commandTasks = new Task[50];
        var deferralTasks = new Task[50];

        // Act - Commands and direct event deferral in parallel
        for (var i = 0; i < 50; i++)
        {
            commandTasks[i] = mediator.Send(new TestCommand { Id = 1 });
            var index = i;
            deferralTasks[i] = Task.Run(() =>
            {
                mediator.DeferEvent(new FakeEvent { Id = index + 1000 });
            });
        }

        await Task.WhenAll(commandTasks);
        await Task.WhenAll(deferralTasks);
        await mediator.ExecuteDeferredEvents();

        // Assert - No exceptions, events from command handlers + deferred events should be processed
        // Command handler defers FakeEvent and FakeEventFromHandler
        eventHandler.Received().Handle(Arg.Any<FakeEvent>()).FireAndForget();
    }

    [TestMethod]
    public async Task StressTest_100KOperations_10Threads_NoRaceConditions()
    {
        // Arrange
        var handlerFactory = new TestHandlerRegistry();
        var mediator = new Mediator(handlerFactory.MultiInstanceFactory, handlerFactory.SingleInstanceFactory);

        var processedEvents = 0;
        var eventHandler = Substitute.For<HandlerWithoutAdditionalEvents>();
        eventHandler.Handle(Arg.Any<FakeEvent>()).Returns(ci =>
        {
            Interlocked.Increment(ref processedEvents);
            return Task.CompletedTask;
        });
        handlerFactory.AddHandlersForEvent<IEventHandler<FakeEvent>>([eventHandler]);

        const int threadsCount = 10;
        const int eventsPerThread = 10000;
        var tasks = new Task[threadsCount];

        // Act
        for (var i = 0; i < threadsCount; i++)
        {
            var threadId = i;
            tasks[i] = Task.Run(() =>
            {
                for (var j = 0; j < eventsPerThread; j++)
                {
                    mediator.DeferEvent(new FakeEvent { Id = (threadId * eventsPerThread) + j });
                }
            });
        }

        await Task.WhenAll(tasks);
        await mediator.ExecuteDeferredEvents();

        // Assert
        Assert.AreEqual(threadsCount * eventsPerThread, processedEvents,
            "All events should be processed without loss");
    }
}
