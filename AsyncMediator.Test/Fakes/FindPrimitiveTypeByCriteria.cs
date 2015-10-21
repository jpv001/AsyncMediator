using System.Linq;
using System.Threading.Tasks;

namespace AsyncMediator.Test
{
    public class FindPrimitiveTypeByCriteria : IQuery<SingleNameCriteria, int>
    {
        public Task<int> Query(SingleNameCriteria criteria)
        {
            var result = FakeDataStore.Results.Single(x => x.Name == criteria.Name);
            return Task.FromResult(result.Id);
        }
    }
}