using System.Windows.Media;
using Dependiator.GitModel;


namespace Dependiator.Common.ThemeHandling
{
	internal interface IThemeService
	{
		Theme Theme { get; }

		Brush GetBranchBrush(Branch branch);
	
		Brush ChangeBranchBrush(Branch branch);

		void SetThemeWpfColors();
	}
}