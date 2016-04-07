using System;

namespace AsyncMediator
{
    /// <summary>
    /// The HandleOrder Attribute can be used to force handlers to fire in a specific order.
    /// Order is not guaranteed when adding across threads due to <see cref="System.Collections.Concurrent.ConcurrentBag{T}"/> (within a single thread it will be ordered)
    /// <see cref="System.Collections.Concurrent.ConcurrentBag{T}"/> adds and removes on a per thread basis first to avoid syncs and locks between threads
    /// </summary>
    [Obsolete("This is to be removed in future versions, re-think the events and consider chaining them rather than depending on handler ordering - Order is not guaranteed when adding across threads due to ConcurrentBag (within a single thread it will be ordered)")]
    public class HandlerOrderAttribute : Attribute
    {
        /// <summary>
        /// The order value.
        /// </summary>
        public int Value { get; set; }

        /// <summary>
        /// Public constructor.
        /// </summary>
        /// <param name="value">The order value.</param>
        public HandlerOrderAttribute(int value)
        {
            Value = value;
        }
    }
}