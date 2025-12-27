namespace AsyncMediator.Tests.Fakes;

public sealed class FakeEvent : IFakeEvent
{
    public int Id { get; set; }
}

public interface IFakeEvent : IDomainEvent
{
}

public sealed class FakeEventFromHandler : IDomainEvent
{
    public int Id { get; set; }
}

public sealed class FakeEventTwoFromHandler : IDomainEvent
{
    public int Id { get; set; }
}
