using System.Threading.Tasks;

namespace AsyncMediator.Test
{
    [HandlerOrder(1)]
    public class HandlerForInterfaceWithoutAdditionalEvents : IEventHandler<IFakeEvent>
    {
        public virtual Task Handle(IFakeEvent @event)
        {
            return Task.FromResult(3);
        }
    }
}