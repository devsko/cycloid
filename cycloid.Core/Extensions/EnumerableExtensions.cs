#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace System.Linq;
#pragma warning restore IDE0130 // Namespace does not match folder structure

public static class EnumerableExtensions
{
    public static TSource? MinBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> selector, TSource? defaultValue = default, IComparer<TKey>? comparer = null)
    {
        comparer ??= Comparer<TKey>.Default;

        using IEnumerator<TSource> sourceIterator = source.GetEnumerator();
        if (!sourceIterator.MoveNext())
        {
            return defaultValue;
        }
        TSource? min = sourceIterator.Current;
        TKey? minKey = selector(min);
        while (sourceIterator.MoveNext())
        {
            TSource? candidate = sourceIterator.Current;
            TKey? candidateProjected = selector(candidate);
            if (comparer.Compare(candidateProjected, minKey) < 0)
            {
                min = candidate;
                minKey = candidateProjected;
            }
        }
        return min;
    }

    public static (TSource?, TKey?) MinByWithKey<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> selector, IComparer<TKey>? comparer = null)
    {
        comparer ??= Comparer<TKey>.Default;

        using IEnumerator<TSource> sourceIterator = source.GetEnumerator();
        if (!sourceIterator.MoveNext())
        {
            return (default, default);
        }
        TSource? min = sourceIterator.Current;
        TKey? minKey = selector(min);
        while (sourceIterator.MoveNext())
        {
            TSource? candidate = sourceIterator.Current;
            TKey? candidateProjected = selector(candidate);
            if (comparer.Compare(candidateProjected, minKey) < 0)
            {
                min = candidate;
                minKey = candidateProjected;
            }
        }
        return (min, minKey);
    }

    public static IEnumerable<(T Previous, T Current)> InPairs<T>(this IEnumerable<T> source)
    {
        IEnumerator<T> enumerator = source.GetEnumerator();
        if (enumerator.MoveNext())
        {
            T previous = enumerator.Current;
            while (enumerator.MoveNext())
            {
                T current = enumerator.Current;
                yield return (previous, current);
                previous = current;
            }
        }
    }
}
