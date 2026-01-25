namespace DependinatorCore.Utils;

public static class Sorter
{
    // Bubble sort
    public static void Sort<T>(IList<T> list, Func<T, T, int> comparer)
    {
        for (int i = 0; i < list.Count - 1; i++)
        {
            for (int j = 0; j < list.Count - i - 1; j++)
            {
                if (comparer(list[j], list[j + 1]) > 0)
                {
                    (list[j + 1], list[j]) = (list[j], list[j + 1]);
                }
            }
        }
    }
}
