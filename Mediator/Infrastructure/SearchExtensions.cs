using System.Collections.Generic;

namespace AsyncMediator
{

    public static class SearchExtensions
    {
        public static TResult Map<TSource, TResult>(this TSource source)
            where TResult : class, IHaveViewModel<TResult, TSource>, new()
            where TSource : class
        {
            if (source == null) return null;
            var result = new TResult();
            return result.ToViewModel(source);
        }

        public static List<TResult> Map<TSource, TResult>(this List<TSource> source)
            where TResult : class, IHaveViewModel<List<TResult>, List<TSource>>, new()
            where TSource : class
        {
            if (source == null) return null;
            var result = new TResult();
            return result.ToViewModel(source);
        }
    }
}
