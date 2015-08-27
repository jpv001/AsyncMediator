using System;
using System.Collections.Generic;

namespace AsyncMediator
{
    public delegate IEnumerable<object> MultiInstanceFactory(Type serviceType);
}