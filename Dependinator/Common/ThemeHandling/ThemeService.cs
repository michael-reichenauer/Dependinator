using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Dependinator.Common.ModelMetadataFolders;
using Dependinator.Common.SettingsHandling;
using Dependinator.Utils.Dependencies;


namespace Dependinator.Common.ThemeHandling
{
    [SingleInstance]
    internal class ThemeService : IThemeService
    {
        private readonly Dictionary<string, Brush> customBranchBrushes = new Dictionary<string, Brush>();
        private readonly ISettingsService settingsService;


        public ThemeService(ISettingsService settingsService, ModelMetadata modelMetadata)
        {
            this.settingsService = settingsService;

            settingsService.EnsureExists<Options>();

            LoadTheme();

            LoadCustomBranchColors();

            modelMetadata.OnChange += (s, e) => LoadCustomBranchColors();
        }


        public Theme Theme { get; private set; }


        public Brush GetBranchBrush(string name)
        {
            //if (branch.IsMultiBranch)
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

            return Theme.GetBrush(name);
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
            int index = Theme.GetBrushIndex(currentBrush);

            // Select next brush
            int newIndex = (index + 1) % (Theme.brushes.Count - 2) + 2;

            Brush brush = Theme.brushes[newIndex];
            string brushHex = Converter.HexFromBrush(brush);

            settingsService.Edit<WorkFolderSettings>(s => s.BranchColors[name] = brushHex);

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


        public SolidColorBrush GetRectangleBrush(string nodeName)
        {
            int code = Math.Abs(nodeName?.GetHashCode() ?? 0);
            int index = code % Theme.brushes.Count;
            return Theme.brushes[index];
        }


        public Brush GetRectangleBackgroundBrush(Brush brush) => Theme.GetDarkerBrush(brush);

        public Brush GetRectangleSelectedBackgroundBrush(Brush brush) => Theme.GetSelectedBrush(brush);


        public Brush BackgroundBrush() => Theme.BackgroundBrush;
        public Brush GetTextBrush() => Theme.TextBrush;
        public Brush GetTextLowBrush() => Theme.TextLowBrush;
        public Brush GetDimBrush() => Theme.DimBrush;


        public Brush GetTextDimBrush() => Theme.DimBrush;


        public Brush GetRectangleHighlighterBrush(Brush brush) => Theme.GetLighterLighterBrush(brush);


        private void LoadTheme()
        {
            ThemeOption themeOption = GetCurrentThemeOption();

            Theme = new Theme(themeOption);
        }


        private ThemeOption GetCurrentThemeOption()
        {
            Options options = settingsService.Get<Options>();

            ThemeOption theme = options.Themes.CustomThemes
                                    .FirstOrDefault(t => t.Name == options.Themes.CurrentTheme)
                                ?? options.Themes.DefaultTheme;

            return theme;
        }


        private void LoadCustomBranchColors()
        {
            customBranchBrushes.Clear();
            WorkFolderSettings folderSettings = settingsService.Get<WorkFolderSettings>();

            foreach (var pair in folderSettings.BranchColors)
            {
                Brush brush = Theme.brushes.FirstOrDefault(b => Converter.HexFromBrush(b) == pair.Value);
                if (brush != null)
                {
                    customBranchBrushes[pair.Key] = brush;
                }
            }
        }
    }
}
