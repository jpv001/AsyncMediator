using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AsyncMediator.Test
{
    public class FindResultForLookup : ILookupQuery<List<FakeResult>>
    {
        public async Task<List<FakeResult>> Query()
        {
            return await Task.FromResult(FakeDataStore.Results.ToList());
        }
    }
}