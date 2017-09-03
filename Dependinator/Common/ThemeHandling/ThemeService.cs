using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Dependinator.Common.SettingsHandling;
using Dependinator.Common.SettingsHandling.Private;
using Dependinator.Common.WorkFolders;
using Dependinator.Utils;


namespace Dependinator.Common.ThemeHandling
{
	[SingleInstance]
	internal class ThemeService : IThemeService
	{
		private readonly ISettings settings;
		private readonly Dictionary<string, Brush> customBranchBrushes = new Dictionary<string, Brush>();

		private int currentIndex = 0;

		private Theme currentTheme;

		public ThemeService(ISettings settings, WorkingFolder workingFolder)
		{
			this.settings = settings;

			settings.EnsureExists<Options>();
			
			LoadTheme();

			LoadCustomBranchColors();

			workingFolder.OnChange += (s, e) => LoadCustomBranchColors();
		}


		public Theme Theme => currentTheme;

		public Brush GetBranchBrush(string name)
		{			//if (branch.IsMultiBranch)
			//{
			//	return currentTheme.GetMultiBranchBrush();
			//}

			//if (branch.Name == BranchName.Master)
			//{
			//	return currentTheme.GetMasterBranchBrush();
			//}


			if (customBranchBrushes.TryGetValue(name, out Brush branchBrush))
			{
				return branchBrush;
			}

			return currentTheme.GetBrush(name);
		}

		public Brush GetBrushFromHex(string hexColor)
		{    
			return Converter.BrushFromHex(hexColor);
		}

		public string GetHexColorFromBrush(Brush brush)
		{
			return Converter.HexFromBrush(brush);
		}

		public Brush ChangeBranchBrush(string name)
		{
			Brush currentBrush = GetBranchBrush(name);
			int index = currentTheme.GetBrushIndex(currentBrush);
	
			// Select next brush
			int newIndex = ((index + 1) % (currentTheme.brushes.Count - 2)) + 2;

			Brush brush = currentTheme.brushes[newIndex];		
			string brushHex = Converter.HexFromBrush(brush);

			settings.Edit<WorkFolderSettings>(s => s.BranchColors[name] = brushHex);

			LoadCustomBranchColors();

			return brush;
		}


		public void SetThemeWpfColors()
		{
			LoadTheme();

			Collection<ResourceDictionary> dictionaries = 
				Application.Current.Resources.MergedDictionaries;

			ResourceDictionary colors = dictionaries
				.First(r => r.Source.ToString() == "Styles/ColorStyle.xaml");

			colors["BackgroundBrush"] = Theme.BackgroundBrush;
			colors["TitlebarBackgroundBrush"] = Theme.TitlebarBackgroundBrush;
			colors["BorderBrush"] = Theme.BorderBrush;
			colors["TextBrush"] = Theme.TextBrush;
			colors["TextLowBrush"] = Theme.TextLowBrush;
			colors["TicketBrush"] = Theme.TicketBrush;
			colors["UndoBrush"] = Theme.UndoBrush;
			colors["TagBrush"] = Theme.TagBrush;
			colors["BranchTipsBrush"] = Theme.BranchTipsBrush;
			colors["CurrentCommitIndicatorBrush"] = Theme.CurrentCommitIndicatorBrush;
			colors["RemoteAheadBrush"] = Theme.RemoteAheadBrush;
			colors["LocalAheadBrush"] = Theme.LocalAheadBrush;
			colors["BusyBrush"] = Theme.BusyBrush;
			colors["ScrollbarBrush"] = Theme.ScrollbarBrush;
			colors["ConflictBrush"] = Theme.ConflictBrush;
			colors["UncomittedBrush"] = Theme.UnCommittedBrush;

			colors["ItemBrush"] = Theme.ItemBackgroundBrush;
			colors["SelectedItemBorderBrush"] = Theme.SelectedItemBorderBrush;
			colors["SelectedItemBackgroundBrush"] = Theme.SelectedItemBackgroundBrush;
			colors["HoverItemBrush"] = Theme.HoverItemBrush;

		}


		public SolidColorBrush GetRectangleBrush()
		{
			int index = (currentIndex++) % Theme.brushes.Count;
			
			return Theme.brushes[index];
		}


		public Brush GetRectangleBackgroundBrush(Brush brush)
		{
			return Theme.GetDarkerBrush(brush);
		}


		public Brush GetRectangleHighlighterBrush(Brush brush)
		{
			return Theme.GetLighterLighterBrush(brush);
		}



		private void LoadTheme()
		{
			ThemeOption themeOption = GetCurrentThemeOption();

			currentTheme = new Theme(themeOption);
		}

		
		private ThemeOption GetCurrentThemeOption()
		{
			Options options = settings.Get<Options>();

			ThemeOption theme = options.Themes.CustomThemes
				.FirstOrDefault(t => t.Name == options.Themes.CurrentTheme)
				?? options.Themes.DefaultTheme;

			return theme;
		}


		private void LoadCustomBranchColors()
		{
			customBranchBrushes.Clear();
			WorkFolderSettings folderSettings = settings.Get<WorkFolderSettings>();

			foreach (var pair in folderSettings.BranchColors)
			{
				Brush brush = currentTheme.brushes.FirstOrDefault(b => Converter.HexFromBrush(b) == pair.Value);
				if (brush != null)
				{
					customBranchBrushes[pair.Key] = brush;
				}
			}
		}
	}
}