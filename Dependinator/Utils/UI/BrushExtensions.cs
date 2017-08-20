using Dependinator.Common.ThemeHandling;

// ReSharper disable once CheckNamespace
namespace System.Windows.Media
{
	internal static class BrushExtensions
	{
		public static string AsString(this Brush brush) => Converter.HexFromBrush(brush);
	}
}
