using System.Threading.Tasks;

namespace AsyncMediator.Test
{
    [HandlerOrder(1)]
    public class HandlerDeferringSingleEvent : IEventHandler<FakeEvent>
    {
        private readonly IMediator _mediator;
        public HandlerDeferringSingleEvent(IMediator mediator)
        {
            _mediator = mediator;
        }

        public virtual Task Handle(FakeEvent @event)
        {
            _mediator.DeferEvent(new FakeEventFromHandler { Id = 1 });
            return Task.FromResult(1);
        }
    }
}