using System.Threading.Tasks;

namespace AsyncMediator.Test
{
    [HandlerOrder(2)]
    public class HandlerWithoutEventsWithOrdering : IEventHandler<FakeEvent>
    {
        public virtual Task Handle(FakeEvent @event)
        {
            return Task.FromResult(5);
        }
    }
}