using System.Windows.Media;


namespace Dependiator.Common.ThemeHandling
{
	internal interface IThemeService
	{
		Theme Theme { get; }

		Brush GetBranchBrush(string name);
	
		Brush ChangeBranchBrush(string name);

		void SetThemeWpfColors();

		SolidColorBrush GetRectangleBrush();
		Brush GetRectangleBackgroundBrush(Brush brush);
	}
}