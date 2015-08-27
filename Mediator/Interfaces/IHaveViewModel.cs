namespace AsyncMediator
{
    public interface IHaveViewModel<out TResult, in TSource>
    {
        TResult ToViewModel(TSource source);
    }
}