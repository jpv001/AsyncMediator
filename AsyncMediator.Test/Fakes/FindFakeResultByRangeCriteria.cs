using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AsyncMediator.Test
{
    public class FindFakeResultByRangeCriteria : IQuery<FakeRangeCriteria, List<FakeResult>>
    {
        public Task<List<FakeResult>> Query(FakeRangeCriteria criteria)
        {
            var result = FakeDataStore.Results.Where(x => x.Id <= criteria.MaxValue && x.Id >= criteria.MinValue).ToList();
            return Task.FromResult(result);
        }
    }
}