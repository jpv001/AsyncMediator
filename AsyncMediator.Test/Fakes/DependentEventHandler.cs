using System.Threading.Tasks;

namespace AsyncMediator.Test
{
    public class DependentEventHandler : IEventHandler<FakeEventFromHandler>
    {
        public virtual Task Handle(FakeEventFromHandler @event)
        {
            return Task.FromResult(4);
        }
    }
}