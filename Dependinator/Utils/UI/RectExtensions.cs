using System.Globalization;

// ReSharper disable once CheckNamespace
namespace System.Windows.Media
{
	internal static class RectExtensions
	{
		public static string AsString(this Rect rect) => rect.ToString(CultureInfo.InvariantCulture);
	}
}