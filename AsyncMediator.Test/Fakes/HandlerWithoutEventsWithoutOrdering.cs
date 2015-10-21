using System.Threading.Tasks;

namespace AsyncMediator.Test
{
    public class HandlerWithoutEventsWithoutOrdering : IEventHandler<FakeEvent>
    {
        public virtual Task Handle(FakeEvent @event)
        {
            return Task.FromResult(5);
        }
    }
}