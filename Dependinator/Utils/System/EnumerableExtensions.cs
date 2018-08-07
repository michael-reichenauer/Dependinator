using System.Collections.Generic;


namespace System.Linq
{
    public static class EnumerableExtensions
    {
        public static IEnumerable<T> ForEach<T>(this IEnumerable<T> enumeration, Action<T> action)
        {
            foreach (T item in enumeration)
            {
                action(item);
            }

            return enumeration;
        }

        public static IEnumerable<IEnumerable<T>> Batch<T>(this IEnumerable<T> source, int size)
        {
            using (var enumerator = source.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    int i = 0;

                    // Batch is a local function closing over `i` and `enumerator` that
                    // executes the inner batch enumeration
                    IEnumerable<T> Batch()
                    {
                        do yield return enumerator.Current;
                        while (++i < size && enumerator.MoveNext());
                    }

                    yield return Batch();
                    while (++i < size && enumerator.MoveNext()); // discard skipped items
                }
            }
        }


        public static IReadOnlyList<TSource> AsReadOnlyList<TSource>(this IReadOnlyList<TSource> source)
        {
            return source;
        }


        public static IReadOnlyList<TSource> ToReadOnlyList<TSource>(this IEnumerable<TSource> enumeration)
        {
            return enumeration.ToList();
        }


        /// <summary>
        ///     Returns distinct elements from a sequence by using a specified
        ///     predicate to compare values of two elements.
        /// </summary>
        public static IEnumerable<TSource> Distinct<TSource>(this IEnumerable<TSource> source,
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


        public static IEnumerable<TSource> DistinctBy<TSource, TKey>
            (this IEnumerable<TSource> source, Func<TSource, TKey> keySelector)
        {
            HashSet<TKey> seenKeys = new HashSet<TKey>();
            foreach (TSource element in source)
            {
                if (seenKeys.Add(keySelector(element)))
                {
                    yield return element;
                }
            }
        }


        public static IEnumerable<List<T>> Partition<T>(this IEnumerable<T> sequence, int size)
        {
            List<T> partition = new List<T>(size);
            foreach (var item in sequence)
            {
                partition.Add(item);
                if (partition.Count == size)
                {
                    yield return partition;
                    partition = new List<T>(size);
                }
            }

            if (partition.Count > 0)
            {
                yield return partition;
            }
        }


        ///<summary>Finds the index of the first item matching an expression in an enumerable.</summary>
        ///<param name="items">The enumerable to search.</param>
        ///<param name="predicate">The expression to test the items against.</param>
        ///<returns>The index of the first matching item, or -1 if no items match.</returns>
        public static int FindIndex<T>(this IEnumerable<T> items, Func<T, bool> predicate)
        {
            if (items == null) throw new ArgumentNullException("items");
            if (predicate == null) throw new ArgumentNullException("predicate");

            int retVal = 0;
            foreach (var item in items)
            {
                if (predicate(item)) return retVal;
                retVal++;
            }

            return -1;
        }


        ///<summary>Finds the index of the first occurrence of an item in an enumerable.</summary>
        ///<param name="items">The enumerable to search.</param>
        ///<param name="item">The item to find.</param>
        ///<returns>The index of the first matching item, or -1 if the item was not found.</returns>
        public static int IndexOf<T>(this IEnumerable<T> items, T item)
        {
            return items.FindIndex(i => EqualityComparer<T>.Default.Equals(item, i));
        }


        private class DistinctComparer<TSource> : IEqualityComparer<TSource>
        {
            private readonly Func<TSource, TSource, bool> comparer;


            public DistinctComparer(Func<TSource, TSource, bool> comparer)
            {
                this.comparer = comparer;
            }


            public bool Equals(TSource x, TSource y) => comparer(x, y);


            // Always returns 0 to force the Distinct comparer function to call the Equals() function
            // to do the comparison
            public int GetHashCode(TSource obj) => 0;
        }
    }
}
