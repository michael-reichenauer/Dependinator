using System.Collections.Generic;


namespace System.Linq
{
	public static class CollectionExtensions
	{
		public static bool TryAdd<T>(this ICollection<T> collection, T item)
		{
			if (!collection.Contains(item))
			{
				collection.Add(item);
				return true;
			}

			return false;
		}
	}
}