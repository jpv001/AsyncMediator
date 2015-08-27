using System.Threading.Tasks;

namespace AsyncMediator
{
    /// <summary>
    /// This is used as a query handler to receive queries from the <see cref="IMediator"/> and return a <see cref="Task"/> containing the result
    /// </summary>
    /// <typeparam name="TCriteria">The <see cref="System.Type"/> of criteria object you wish to pass in</typeparam>
    /// <typeparam name="TResult">The <see cref="System.Type"/> of result that you want to retrieve</typeparam>
    public interface IQuery<in TCriteria, TResult>
    {
        Task<TResult> Query(TCriteria criteria);
    }
}