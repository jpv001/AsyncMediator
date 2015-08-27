using System.Threading.Tasks;

namespace AsyncMediator
{
    /// <summary>
    /// This is used as a query handler to receive queries from the <see cref="IMediator"/>, that do not require criteria, and return a <see cref="Task"/> containing the result
    /// </summary>
    /// <typeparam name="TResult">The <see cref="System.Type"/> of result that you want to retrieve</typeparam>
    public interface ILookupQuery<TResult>
    {
        Task<TResult> Query();
    }
}