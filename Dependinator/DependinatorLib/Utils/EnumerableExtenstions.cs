namespace System.Linq;

// Some useful IEnumerable extensions that are missing in .NET
public static class EnumerableExtensions
{
    // Calls acton for each item in the enumeration
    public static void ForEach<T>(this IEnumerable<T> enumeration, Action<T> action)
    {
        foreach (T item in enumeration)
        {
            action(item);
        }
    }

    // Calls acton for each item in the enumeration (includes index)
    public static void ForEach<T>(this IEnumerable<T> enumeration, Action<T, int> action)
    {
        int i = 0;
        foreach (T item in enumeration)
        {
            action(item, i++);
        }
    }


    // Returns a concatenated string of all items in the string enumeration
    // Same result as calling string.Join(separator, source)
    public static string Join(this IEnumerable<string> source, string separator)
    {
        return string.Join(separator, source);
    }

    // Returns a concatenated string of all items in the enumeration after applying transform
    // Same result as calling string.Join(separator, source.Select(transform)
    public static string JoinBy<TSource>(this IEnumerable<TSource> source, Func<TSource, string> transform, string separator)
    {
        return string.Join(separator, source.Select(transform));
    }

    // Returns a concatenated string of all items in the string enumeration 
    // Same result as calling string.Join(separator, source)
    public static string Join(this IEnumerable<string> source, char separator)
    {
        return string.Join(separator, source);
    }

    // Returns a concatenated string of all items in the string enumeration after applying transform
    // Same result as calling string.Join(separator, source)
    public static string Join<TSource>(this IEnumerable<TSource> source, Func<TSource, string> transform, char separator)
    {
        return string.Join(separator, source.Select(transform));
    }

    // Tries to add the item to the list if it does not already exist
    public static void TryAdd<TSource>(this List<TSource> source, TSource item)
    {
        if (source.Contains(item))
        {
            return;
        }
        source.Add(item);
    }

    // Tries to add all the items to the list if they do not already exist
    public static void TryAddAll<TSource>(this List<TSource> source, IEnumerable<TSource> items)
    {
        foreach (var item in items)
        {
            if (source.Contains(item))
            {
                continue;
            }
            source.Add(item);
        }
    }

    // Tries to add the item to the list if it does not already exist
    public static void TryAddBy<TSource>(this List<TSource> source, Func<TSource, bool> predicate, TSource item)
    {
        if (null != source.FirstOrDefault(predicate))
        {
            return;
        }
        source.Add(item);
    }

    // Returns true if the enumeration contains the item
    public static bool ContainsBy<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
    {
        return null != source.FirstOrDefault(predicate);
    }

    // Returns the index of the first item that matches the predicate
    public static int FindIndexBy<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
    {
        var index = 0;
        foreach (var item in source)
        {
            if (predicate(item))
            {
                return index;
            }
            index++;
        }
        return -1;
    }

    // Returns the index of the last item that matches the predicate
    public static int FindLastIndexBy<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
    {
        var index = 0;
        var reverseSource = source.Reverse();
        foreach (var item in reverseSource)
        {
            if (predicate(item))
            {
                return source.Count() - 1 - index;
            }
            index++;
        }
        return -1;
    }


    // Returns elements from a sequence by concatenating the params parameters
    public static IEnumerable<TSource> Add<TSource>(this IEnumerable<TSource> source, params TSource[] items)
    {
        return source.Concat(items);
    }


    // Returns distinct elements from a sequence by using a specified 
    // predicate to compare values of two elements.
    public static IEnumerable<TSource> DistinctBy<TSource>(this IEnumerable<TSource> source,
        Func<TSource, TSource, bool> comparer)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (comparer == null)
        {
            throw new ArgumentNullException(nameof(comparer));
        }

        // Use the MSDN provided Distinct function with a private custom IEqualityComparer comparer.
        return source.Distinct(new DistinctComparer<TSource>(comparer));
    }


    class DistinctComparer<TSource> : IEqualityComparer<TSource>
    {
        private readonly Func<TSource, TSource, bool> comparer;

        public DistinctComparer(Func<TSource, TSource, bool> comparer)
        {
            this.comparer = comparer;
        }

#pragma warning disable CS8767
        public bool Equals(TSource x, TSource y) => comparer(x, y);
#pragma warning restore CS8767

        // Always returns 0 to force the Distinct comparer function to call the Equals() function
        // to do the comparison
        public int GetHashCode(TSource obj) => 0;
    }
}
