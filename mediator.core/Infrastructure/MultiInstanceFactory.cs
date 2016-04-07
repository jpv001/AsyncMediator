using System;
using System.Collections.Generic;

namespace AsyncMediator
{
    /// <summary>
    /// A delegate for creating multiple instances of a service type.
    /// </summary>
    /// <param name="serviceType">The service type.</param>
    /// <returns>A list of service instances.</returns>
    public delegate IEnumerable<object> MultiInstanceFactory(Type serviceType);
}