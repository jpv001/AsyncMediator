using System;

namespace AsyncMediator
{
    /// <summary>
    /// The HandleOrder Attribute can be used to force handlers to fire in a specific order
    /// </summary>
    [Obsolete("This is to be removed in future versions, re-think the events and consider chaining them rather than depending on handler ordering")]
    public class HandlerOrderAttribute : Attribute
    {
        public int Value { get; set; }

        public HandlerOrderAttribute(int value)
        {
            Value = value;
        }
    }
}