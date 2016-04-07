using System;

namespace AsyncMediator
{
    /// <summary>
    /// A delegate for creating a single instance of a service type.
    /// </summary>
    /// <param name="serviceType">The service type.</param>
    /// <returns>The service instance.</returns>
    public delegate object SingleInstanceFactory(Type serviceType);
}