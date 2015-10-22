using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AsyncMediator.Test
{
    public class FindFakeResultByPrimitiveType : IQuery<int, List<FakeResult>>
    {
        public Task<List<FakeResult>> Query(int criteria)
        {
            var result = FakeDataStore.Results.Where(x => x.Id == criteria).ToList();
            return Task.FromResult(result);
        }
    }
}