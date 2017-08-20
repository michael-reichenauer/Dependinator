// ReSharper disable once CheckNamespace
namespace System.Windows.Media
{
	internal static class BrushExtensions
	{
		public static string AsString(this Brush brush)
		{
			if (brush == null)
			{
				return null;
			}

			return (string)new BrushConverter().ConvertTo(brush, typeof(string));
		}
	}
}
