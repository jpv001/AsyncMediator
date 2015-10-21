using System.Collections.Generic;
using System.Linq;
using FizzWare.NBuilder;

namespace AsyncMediator.Test
{
    public class FakeDataStore
    {
        public static readonly List<FakeResult> Results = Builder<FakeResult>.CreateListOfSize(10).Build().ToList();
    }
}