namespace AsyncMediator.Tests.Fakes;

/// <summary>
/// Event handler that defers multiple events during handling.
/// Note: Handler ordering was removed in v3.0.0 - use DI registration order instead.
/// </summary>
public class HandlerDeferringMultipleEvents(IMediator mediator) : IEventHandler<FakeEvent>
{
    public virtual Task Handle(FakeEvent @event, CancellationToken cancellationToken = default)
    {
        mediator.DeferEvent(new FakeEventFromHandler { Id = 1 });
        mediator.DeferEvent(new FakeEventTwoFromHandler { Id = 1 });
        return Task.CompletedTask;
    }
}

/// <summary>
/// Event handler that defers a single event during handling.
/// </summary>
public class HandlerDeferringSingleEvent(IMediator mediator) : IEventHandler<FakeEvent>
{
    public virtual Task Handle(FakeEvent @event, CancellationToken cancellationToken = default)
    {
        mediator.DeferEvent(new FakeEventFromHandler { Id = 1 });
        return Task.CompletedTask;
    }
}

public class HandlerWithoutAdditionalEvents : IEventHandler<FakeEvent>
{
    public virtual Task Handle(FakeEvent @event, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public class HandlerWithoutEventsWithoutOrdering : IEventHandler<FakeEvent>
{
    public virtual Task Handle(FakeEvent @event, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public class DependentEventHandler : IEventHandler<FakeEventFromHandler>
{
    public virtual Task Handle(FakeEventFromHandler @event, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public class ChainedEventHandler : IEventHandler<FakeEventTwoFromHandler>
{
    public virtual Task Handle(FakeEventTwoFromHandler @event, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

// Interface-based event handlers

/// <summary>
/// Event handler for interface-based events that defers multiple events.
/// </summary>
public class HandlerForInterfaceDeferringMultipleEvents(IMediator mediator) : IEventHandler<IFakeEvent>
{
    public virtual Task Handle(IFakeEvent @event, CancellationToken cancellationToken = default)
    {
        mediator.DeferEvent(new FakeEventFromHandler { Id = 1 });
        mediator.DeferEvent(new FakeEventTwoFromHandler { Id = 1 });
        return Task.CompletedTask;
    }
}

/// <summary>
/// Event handler for interface-based events that defers a single event.
/// </summary>
public class HandlerForInterfaceDeferringSingleEvent(IMediator mediator) : IEventHandler<IFakeEvent>
{
    public virtual Task Handle(IFakeEvent @event, CancellationToken cancellationToken = default)
    {
        mediator.DeferEvent(new FakeEventFromHandler { Id = 1 });
        return Task.CompletedTask;
    }
}

public class HandlerForInterfaceWithoutAdditionalEvents : IEventHandler<IFakeEvent>
{
    public virtual Task Handle(IFakeEvent @event, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
