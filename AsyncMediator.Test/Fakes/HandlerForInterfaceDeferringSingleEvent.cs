using System.Threading.Tasks;

namespace AsyncMediator.Test
{
    [HandlerOrder(1)]
    public class HandlerForInterfaceDeferringSingleEvent : IEventHandler<IFakeEvent>
    {
        private readonly IMediator _mediator;

        public HandlerForInterfaceDeferringSingleEvent(IMediator mediator)
        {
            _mediator = mediator;
        }

        public virtual Task Handle(IFakeEvent @event)
        {
            _mediator.DeferEvent(new FakeEventFromHandler { Id = 1 });
            return Task.FromResult(1);
        }
    }
}