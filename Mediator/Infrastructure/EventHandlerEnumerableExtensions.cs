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
        public static IOrderedEnumerable<IEventHandler<TEvent>> OrderByExecutionOrder<TEvent>(
            this IEnumerable<IEventHandler<TEvent>> source) where TEvent : IDomainEvent
        {
            return source.OrderBy(x => x.GetType().GetCustomAttribute<HandlerOrderAttribute>() != null
                ? x.GetType().GetCustomAttribute<HandlerOrderAttribute>().Value
                : 0);
        }
    }

#pragma warning restore 618

}