namespace AsyncMediator.Test
{
    public class FakeEvent : IFakeEvent
    {
        public int Id { get; set; }
    }

    public interface IFakeEvent : IDomainEvent
    {
    }
}