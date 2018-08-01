using System.Globalization;


namespace System.Windows.Media
{
    internal static class SizeExtensions
    {
        public static string AsString(this Size size) =>
            size.ToString(CultureInfo.InvariantCulture);


        public static bool Same(this Size left, Size right) =>
            left.Width.Same(right.Width) && left.Height.Same(right.Height);
    }
}
