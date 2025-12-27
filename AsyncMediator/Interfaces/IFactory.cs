namespace AsyncMediator;

/// <summary>
/// Internal factory interface for creating handler instances.
/// </summary>
public interface IFactory
{
    /// <summary>
    /// Passes a generic type to the <see cref="MultiInstanceFactory"/> to return an <see cref="IEnumerable{T}"/> of <see cref="IEventHandler{TEvent}"/>.
    /// </summary>
    /// <typeparam name="T">A generic type of <see cref="IDomainEvent"/>.</typeparam>
    /// <returns>An <see cref="IEnumerable{T}"/> of <see cref="IEventHandler{TEvent}"/>.</returns>
    IEnumerable<T> CreateEnumerableOf<T>() where T : class;

    /// <summary>
    /// Passes a generic type to the <see cref="SingleInstanceFactory"/> to return a matching <see cref="ICommandHandler{TCommand}"/> for a <see cref="ICommand"/>, <see cref="IQuery{TCriteria,TResult}"/> or <see cref="ILookupQuery{TResult}"/>.
    /// </summary>
    /// <typeparam name="T">A generic type of <see cref="ICommand"/>, <see cref="IQuery{TCriteria,TResult}"/> or <see cref="ILookupQuery{TResult}"/>.</typeparam>
    /// <returns>A singular <see cref="ICommandHandler{TCommand}"/>.</returns>
    T Create<T>() where T : class;
}
