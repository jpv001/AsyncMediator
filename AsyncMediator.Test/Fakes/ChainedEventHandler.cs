using System.Threading.Tasks;

namespace AsyncMediator.Test
{
    public class ChainedEventHandler : IEventHandler<FakeEventTwoFromHandler>
    {
        public virtual Task Handle(FakeEventTwoFromHandler @event)
        {
            return Task.FromResult(6);
        }
    }
}