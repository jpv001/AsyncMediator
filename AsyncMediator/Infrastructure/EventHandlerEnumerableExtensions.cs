using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace AsyncMediator
{
#pragma warning disable 618

    /// <summary>
    /// This is a support class to allow for handler ordering, this will be removed following the removal of <see cref="HandlerOrderAttribute"/>
    /// </summary>
    public static class EventHandlerEnumerableExtensions
    {
        /// <summary>
        /// Orders event handlers by the values specified by their <see cref="HandlerOrderAttribute"/>, if present.
        /// </summary>
        /// <typeparam name="TEvent">The event type.</typeparam>
        /// <param name="source">The source collection of event handlers.</param>
        /// <returns>An ordered collection of event handlers.</returns>
        public static IOrderedEnumerable<IEventHandler<TEvent>> OrderByExecutionOrder<TEvent>(
            this IEnumerable<IEventHandler<TEvent>> source) where TEvent : IDomainEvent
        {
            return source.OrderBy(x =>
            {
                var attr = x.GetType().GetCustomAttribute<HandlerOrderAttribute>();
                return (attr != null) ? attr.Value : 0;
            });
        }
    }

#pragma warning restore 618
}