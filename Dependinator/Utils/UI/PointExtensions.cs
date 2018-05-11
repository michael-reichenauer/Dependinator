using System.Globalization;

// ReSharper disable once CheckNamespace
namespace System.Windows.Media
{
	internal static class PointExtensions
	{
		public static string AsString(this Point point) =>
			point.ToString(CultureInfo.InvariantCulture);

		public static bool Same(this Point left, Point right) =>
			left.X.Same(right.X) && left.Y.Same(right.Y);
	}
}