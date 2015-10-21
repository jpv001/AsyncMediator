using System.Linq;
using System.Threading.Tasks;

namespace AsyncMediator.Test
{
    public class MultipleQueryTypesInOneObject : IQuery<SingleIdCriteria, FakeResult>, IQuery<SingleNameCriteria, FakeResult>
    {
        public Task<FakeResult> Query(SingleIdCriteria criteria)
        {
            var result = FakeDataStore.Results.SingleOrDefault(x => x.Id == criteria.Id);
            return Task.FromResult(result);
        }

        public Task<FakeResult> Query(SingleNameCriteria criteria)
        {
            var result = FakeDataStore.Results.SingleOrDefault(x => x.Name == criteria.Name);
            return Task.FromResult(result);
        }
    }
}