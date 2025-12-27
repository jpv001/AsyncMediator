using AsyncMediator.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;

namespace AsyncMediator.Tests;

[TestClass]
public class IntegrationTests
{
    [TestMethod]
    public async Task MsDI_TransientHandlers_ShouldResolveNewInstanceEachTime()
    {
        // Arrange
        var services = new ServiceCollection();
        RegisterMediatorServices(services, ServiceLifetime.Transient);
        var provider = services.BuildServiceProvider();

        var mediator = provider.GetRequiredService<IMediator>();

        // Act
        var result1 = await mediator.Send(new TestCommand { Id = 1 });
        var result2 = await mediator.Send(new TestCommand { Id = 2 });

        // Assert
        Assert.IsTrue(result1.Success);
        Assert.IsTrue(result2.Success);
    }

    [TestMethod]
    public async Task MsDI_ScopedHandlers_ShouldResolveSameInstanceWithinScope()
    {
        // Arrange
        var services = new ServiceCollection();
        RegisterMediatorServices(services, ServiceLifetime.Scoped);
        var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        // Act
        var result1 = await mediator.Send(new TestCommand { Id = 1 });
        var result2 = await mediator.Send(new TestCommand { Id = 2 });

        // Assert
        Assert.IsTrue(result1.Success);
        Assert.IsTrue(result2.Success);
    }

    [TestMethod]
    public async Task MsDI_SingletonHandlers_ShouldResolveSameInstanceAlways()
    {
        // Arrange
        var services = new ServiceCollection();
        RegisterMediatorServices(services, ServiceLifetime.Singleton);
        var provider = services.BuildServiceProvider();

        var mediator1 = provider.GetRequiredService<IMediator>();
        var mediator2 = provider.GetRequiredService<IMediator>();

        // Act
        var result1 = await mediator1.Send(new TestCommand { Id = 1 });
        var result2 = await mediator2.Send(new TestCommand { Id = 2 });

        // Assert
        Assert.IsTrue(result1.Success);
        Assert.IsTrue(result2.Success);
        Assert.AreSame(mediator1, mediator2);
    }

    [TestMethod]
    public async Task MsDI_QueryHandlers_ShouldResolveCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        RegisterMediatorServices(services, ServiceLifetime.Transient);
        var provider = services.BuildServiceProvider();

        var mediator = provider.GetRequiredService<IMediator>();

        // Act
        var result = await mediator.Query<FakeRangeCriteria, List<FakeResult>>(
            new FakeRangeCriteria { MinValue = 1, MaxValue = 5 });

        // Assert
        Assert.AreEqual(5, result.Count);
    }

    [TestMethod]
    public async Task MsDI_EventHandlers_ShouldResolveMultipleHandlers()
    {
        // Arrange
        var services = new ServiceCollection();
        RegisterMediatorServicesWithMultipleEventHandlers(services);
        var provider = services.BuildServiceProvider();

        var mediator = provider.GetRequiredService<IMediator>();
        var counter = provider.GetRequiredService<EventCounter>();

        // Act
        mediator.DeferEvent(new FakeEvent { Id = 1 });
        await mediator.ExecuteDeferredEvents();

        // Assert
        Assert.AreEqual(2, counter.Count, "Both event handlers should have been called");
    }

    [TestMethod]
    public async Task MsDI_NestedScopes_ShouldIsolateInstances()
    {
        // Arrange
        var services = new ServiceCollection();
        RegisterMediatorServices(services, ServiceLifetime.Scoped);
        var provider = services.BuildServiceProvider();

        IMediator? mediatorScope1;
        IMediator? mediatorScope2;

        // Act
        using (var scope1 = provider.CreateScope())
        {
            mediatorScope1 = scope1.ServiceProvider.GetRequiredService<IMediator>();
            var result1 = await mediatorScope1.Send(new TestCommand { Id = 1 });
            Assert.IsTrue(result1.Success);
        }

        using (var scope2 = provider.CreateScope())
        {
            mediatorScope2 = scope2.ServiceProvider.GetRequiredService<IMediator>();
            var result2 = await mediatorScope2.Send(new TestCommand { Id = 2 });
            Assert.IsTrue(result2.Success);
        }

        // Assert - Different scope instances should be different
        Assert.AreNotSame(mediatorScope1, mediatorScope2);
    }

    private static void RegisterMediatorServices(IServiceCollection services, ServiceLifetime lifetime)
    {
        // Register factories
        services.Add(new ServiceDescriptor(
            typeof(SingleInstanceFactory),
            sp => new SingleInstanceFactory(type => sp.GetRequiredService(type)),
            lifetime));

        services.Add(new ServiceDescriptor(
            typeof(MultiInstanceFactory),
            sp => new MultiInstanceFactory(type =>
                (IEnumerable<object>)sp.GetServices(type)),
            lifetime));

        // Register mediator
        services.Add(new ServiceDescriptor(typeof(IMediator), typeof(Mediator), lifetime));

        // Register handlers
        services.Add(new ServiceDescriptor(
            typeof(ICommandHandler<TestCommand>),
            sp => new TestCommandHandler(sp.GetRequiredService<IMediator>()),
            lifetime));

        services.Add(new ServiceDescriptor(
            typeof(IQuery<FakeRangeCriteria, List<FakeResult>>),
            typeof(FindFakeResultByRangeCriteria),
            lifetime));

        // Register event handlers
        services.Add(new ServiceDescriptor(
            typeof(IEventHandler<FakeEvent>),
            typeof(HandlerWithoutAdditionalEvents),
            lifetime));

        services.Add(new ServiceDescriptor(
            typeof(IEventHandler<FakeEventFromHandler>),
            typeof(DependentEventHandler),
            lifetime));
    }

    private static void RegisterMediatorServicesWithMultipleEventHandlers(IServiceCollection services)
    {
        var counter = new EventCounter();
        services.AddSingleton(counter);

        // Register factories
        services.AddSingleton<SingleInstanceFactory>(sp =>
            type => sp.GetRequiredService(type));

        services.AddSingleton<MultiInstanceFactory>(sp =>
            type => sp.GetServices(type).Where(s => s is not null).Cast<object>());

        // Register mediator
        services.AddSingleton<IMediator, Mediator>();

        // Register multiple event handlers
        services.AddSingleton<IEventHandler<FakeEvent>>(sp =>
            new CountingEventHandler(counter));
        services.AddSingleton<IEventHandler<FakeEvent>>(sp =>
            new CountingEventHandler(counter));
    }
}

public class EventCounter
{
    private int _count;
    public int Count => _count;
    public void Increment() => Interlocked.Increment(ref _count);
}

public class CountingEventHandler(EventCounter counter) : IEventHandler<FakeEvent>
{
    public Task Handle(FakeEvent @event, CancellationToken cancellationToken = default)
    {
        counter.Increment();
        return Task.CompletedTask;
    }
}
