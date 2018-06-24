using System.Windows.Media;


namespace Dependinator.Common.ThemeHandling
{
	internal interface IThemeService
	{
		Theme Theme { get; }

		Brush GetBranchBrush(string name);
	
		Brush ChangeBranchBrush(string name);

		void SetThemeWpfColors();

		SolidColorBrush GetRectangleBrush(string nodeName);
		Brush GetRectangleBackgroundBrush(Brush brush);
		string GetHexColorFromBrush(Brush brush);
		Brush GetBrushFromHex(string hexColor);
		Brush GetRectangleHighlighterBrush(Brush brush);
		Brush GetRectangleSelectedBackgroundBrush(Brush brush);
		Brush BackgroundBrush();
		Brush GetTextBrush();
		Brush GetTextDimBrush();
		Brush GetTextLowBrush();
		Brush GetDimBrush();
	}
}