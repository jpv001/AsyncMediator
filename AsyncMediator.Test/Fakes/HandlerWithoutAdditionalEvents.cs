using System.Threading.Tasks;

namespace AsyncMediator.Test
{
    [HandlerOrder(1)]
    public class HandlerWithoutAdditionalEvents : IEventHandler<FakeEvent>
    {
        public virtual Task Handle(FakeEvent @event)
        {
            return Task.FromResult(3);
        }
    }
}