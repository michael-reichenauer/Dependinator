namespace System.Windows.Media
{
    internal static class VectorExtensions
    {
        public static Vector Rnd(this Vector vector, double roundTo) =>
            new Vector(vector.X.RoundToNearest(roundTo), vector.Y.RoundToNearest(roundTo));
    }
}
