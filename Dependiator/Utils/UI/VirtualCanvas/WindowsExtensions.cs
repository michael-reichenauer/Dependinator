namespace System.Windows
{
	internal static class WindowsExtensions
	{
		public static string TS(this Rect r)
		{
			return $"{Math.Round(r.X, 0)},{Math.Round(r.Y, 0)},{Math.Round(r.Width, 0)},{Math.Round(r.Height, 0)}";
		}

		public static string TS(this Point p)
		{
			return $"{Math.Round(p.X, 0)},{Math.Round(p.Y, 0)}";
		}
	}
}