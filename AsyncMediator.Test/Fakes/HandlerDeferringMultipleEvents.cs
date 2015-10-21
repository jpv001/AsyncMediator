using System.Threading.Tasks;

namespace AsyncMediator.Test
{
    [HandlerOrder(2)]
    public class HandlerDeferringMultipleEvents : IEventHandler<FakeEvent>
    {
        private readonly IMediator _mediator;

        public HandlerDeferringMultipleEvents(IMediator mediator)
        {
            _mediator = mediator;
        }

        public virtual Task Handle(FakeEvent @event)
        {
            _mediator.DeferEvent(new FakeEventFromHandler { Id = 1 });
            _mediator.DeferEvent(new FakeEventTwoFromHandler { Id = 1 });
            return Task.FromResult(2);
        }
    }
}