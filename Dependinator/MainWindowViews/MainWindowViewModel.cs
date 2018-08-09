using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Dependinator.Common.Installation;
using Dependinator.Common.ModelMetadataFolders;
using Dependinator.Common.ModelMetadataFolders.Private;
using Dependinator.Common.ProgressHandling;
using Dependinator.MainWindowViews.Private;
using Dependinator.ModelViewing;
using Dependinator.ModelViewing.Private.CodeViewing;
using Dependinator.Utils.Dependencies;
using Dependinator.Utils.UI;
using Dependinator.Utils.UI.Mvvm;


namespace Dependinator.MainWindowViews
{
    [SingleInstance]
    internal class MainWindowViewModel : ViewModel, IBusyIndicatorProvider
    {
        private readonly IMainWindowService mainWindowService;
        private readonly ModelMetadata modelMetadata;
        private readonly IModelMetadataService modelMetadataService;
        private readonly IModelViewService modelViewService;
        private readonly IOpenModelService openModelService;
        private readonly IProgressService progress;
        private readonly IRecentModelsService recentModelsService;
        private readonly ISolutionService solutionService;
        private readonly IStartInstanceService startInstanceService;


        internal MainWindowViewModel(
            ModelMetadata modelMetadata,
            ILatestVersionService latestVersionService,
            IMainWindowService mainWindowService,
            ModelViewModel modelViewModel,
            IOpenModelService openModelService,
            IRecentModelsService recentModelsService,
            IModelMetadataService modelMetadataService,
            IStartInstanceService startInstanceService,
            ISolutionService solutionService,
            IModelViewService modelViewService,
            IProgressService progress)
        {
            this.modelMetadata = modelMetadata;

            this.mainWindowService = mainWindowService;
            this.openModelService = openModelService;
            this.recentModelsService = recentModelsService;
            this.modelMetadataService = modelMetadataService;
            this.startInstanceService = startInstanceService;
            this.solutionService = solutionService;
            this.modelViewService = modelViewService;
            this.progress = progress;

            ModelViewModel = modelViewModel;

            modelMetadata.OnChange += (s, e) => Notify(nameof(WorkingFolder));
            latestVersionService.OnNewVersionAvailable += (s, e) => IsNewVersionVisible = true;
            latestVersionService.StartCheckForLatestVersion();
            SearchItems = new ObservableCollection<SearchEntry>();
            ClearSelectionItems();
        }


        public int WindowWith { set => ModelViewModel.Width = value; }

        public bool IsInFilterMode => !string.IsNullOrEmpty(SearchText);


        public bool IsNewVersionVisible { get => Get(); set => Set(value); }

        public string WorkingFolder => modelMetadata.ModelName;

        public string WorkingFolderPath => modelMetadata.ModelFilePath;


        public string Title => $"{modelMetadata.ModelName} - {ProgramInfo.Name}";


        public bool IsModel => !modelMetadataService.IsDefault;
        public bool ShowMinimizeButton => !modelMetadataService.IsDefault;
        public bool ShowMaximizeButton => !modelMetadataService.IsDefault;

        public ObservableCollection<SearchEntry> SearchItems { get; }

        public bool IsSearchDropDown { get => Get(); set => Set(value); }

        public string SearchText
        {
            get => Get();
            set
            {
                Set(value);

                if (string.IsNullOrEmpty(value))
                {
                    Set("");
                    ClearSelectionItems();
                    return;
                }

                if (value == SelectedSearchItem?.Name)
                {
                    Set("");
                    ClearSelectionItems();
                    return;
                }

                IsSearchDropDown = true;
                Set(value);
                var items = modelViewService.Search(value)
                    .Take(21)
                    .OrderBy(nodeName => nodeName.DisplayLongName)
                    .Select(nodeName => new SearchEntry(nodeName.DisplayLongName, nodeName))
                    .ToList();
                SearchItems.Clear();
                items.Take(20).ForEach(item => SearchItems.Add(item));
                if (items.Count > 20)
                {
                    SearchItems.Add(new SearchEntry("...", null));
                }

                if (!items.Any())
                {
                    SearchItems.Add(new SearchEntry("<nothing found>", null));
                }
            }
        }


        public SearchEntry SelectedSearchItem
        {
            get => Get<SearchEntry>();
            set
            {
                Set(value);
                if (value == null || value.NodeName == null)
                {
                    return;
                }

                modelViewService.StartMoveToNode(value.NodeName);
            }
        }

        public ModelViewModel ModelViewModel { get; }


        public string VersionText
        {
            get
            {
                Version version = Version.Parse(ProgramInfo.Version);
                DateTime buildTime = ProgramInfo.GetBuildTime();
                string dateText = buildTime.ToString("yyyy-MM-dd\nHH:mm");
                string text = $"Version: {version.Major}.{version.Minor}\n{dateText}";
                return text;
            }
        }


        public IReadOnlyList<HiddenNodeItem> HiddenNodes => GetHiddenNodes();


        public bool HasResent => recentModelsService.GetModelPaths().Any();
        public bool HasHiddenNodes => modelViewService.GetHiddenNodeNames().Any();

        public Command RefreshCommand => AsyncCommand(ManualRefreshAsync);
        public Command RefreshLayoutCommand => AsyncCommand(ManualRefreshLayoutAsync);

        public Command OpenFileCommand => Command(openModelService.ShowOpenModelDialog);

        public Command OpenStudioCommand => AsyncCommand(solutionService.OpenStudioAsync);


        public Command RunLatestVersionCommand => AsyncCommand(RunLatestVersionAsync);

        public Command FeedbackCommand => Command(mainWindowService.SendFeedback);

        public Command OptionsCommand => Command(mainWindowService.OpenOptions);

        public Command HelpCommand => Command(mainWindowService.OpenHelp);

        public Command MinimizeCommand => Command(Minimize);

        public Command CloseCommand => AsyncCommand(CloseWindowAsync);

        public Command ExitCommand => AsyncCommand(CloseWindowAsync);

        public Command ToggleMaximizeCommand => Command(ToggleMaximize);

        public Command EscapeCommand => Command(Escape);

        public Command ClearFilterCommand => Command(ClearFilter);

        public Command SearchCommand => Command(Search);

        public BusyIndicator Busy => BusyIndicator();


        private void ClearSelectionItems()
        {
            SearchItems.Clear();
            SearchItems.Add(new SearchEntry("", null));
        }


        private IReadOnlyList<HiddenNodeItem> GetHiddenNodes()
        {
            return modelViewService.GetHiddenNodeNames()
                .Select(name => new HiddenNodeItem(name, modelViewService.ShowHiddenNode))
                .ToList();
        }


        public async Task LoadAsync()
        {
            await Task.Yield();
        }


        //public Task CloseWindowAsync() => modelViewService.CloseAsync();

        private Task ManualRefreshAsync() => ManualRefreshAsync(false);

        private Task ManualRefreshLayoutAsync() => ManualRefreshAsync(true);

        private void Search() => mainWindowService.SetSearchFocus();


        public Task ActivateRefreshAsync()
        {
            return modelViewService.ActivateRefreshAsync();
        }


        private async Task ManualRefreshAsync(bool refreshLayout)
        {
            using (progress.ShowBusy())
            {
                await modelViewService.RefreshAsync(refreshLayout);
            }
        }


        private void Escape()
        {
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                SearchText = "";
            }
            else
            {
                Minimize();
            }
        }


        private static void Minimize() =>
            Application.Current.MainWindow.WindowState = WindowState.Minimized;


        private static void ToggleMaximize()
        {
            if (Application.Current.MainWindow.WindowState == WindowState.Maximized)
            {
                Application.Current.MainWindow.WindowState = WindowState.Normal;
            }
            else
            {
                Application.Current.MainWindow.WindowState = WindowState.Maximized;
            }
        }


        private async Task CloseWindowAsync()
        {
            using (progress.ShowDialog("Saving model ..."))
            {
                await modelViewService.CloseAsync();
            }

            Application.Current.Shutdown(0);
        }


        private static void Exit() => Application.Current.Shutdown(0);


        private async Task RunLatestVersionAsync()
        {
            await Task.Yield();

            bool isStarting = startInstanceService.StartInstance(modelMetadataService.ModelFilePath);

            if (isStarting)
            {
                // Newer version is started, close this instance
                Application.Current.Shutdown(0);
            }
        }


        private void ClearFilter()
        {
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                SearchText = "";
            }
        }
    }
}
