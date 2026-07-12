namespace Dependinator.Core.Utils;

// Some useful IEnumerable extensions that are missing in .NET
public static class EnumerableExtensions
{
    // Calls action for each item in the enumeration
    public static void ForEach<T>(this IEnumerable<T> enumeration, Action<T> action)
    {
        foreach (T item in enumeration)
        {
            action(item);
        }
    }

    public static async Task ForEachAsync<T>(this IEnumerable<T> enumeration, Func<T, Task> func)
    {
        foreach (T item in enumeration)
        {
            await func(item);
        }
    }

    // Calls action for each item in the enumeration
    public static async Task ForEachAsync<T>(this IAsyncEnumerable<T> enumeration, Action<T> action)
    {
        await foreach (T item in enumeration)
        {
            action(item);
        }
    }

    public static async Task<List<T>> ToListAsync<T>(this IAsyncEnumerable<T> enumeration)
    {
        var list = new List<T>();
        await enumeration.ForEachAsync(list.Add);
        return list;
    }

    // Calls action for each item in the enumeration (includes index)
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

    // Returns true if the enumeration contains an item that matches the predicate
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
}
