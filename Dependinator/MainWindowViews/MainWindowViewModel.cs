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
using Dependinator.ModelViewing;
using Dependinator.ModelViewing.Open;
using Dependinator.ModelViewing.Searching;
using Dependinator.Utils.Dependencies;
using Dependinator.Utils.UI;
using Dependinator.Utils.UI.Mvvm;
using Application = System.Windows.Application;


namespace Dependinator.MainWindowViews
{
	[SingleInstance]
	internal class MainWindowViewModel : ViewModel, IBusyIndicatorProvider
	{
		private readonly IMainWindowService mainWindowService;
		private readonly IOpenModelService openModelService;
		private readonly IRecentModelsService recentModelsService;
		private readonly IModelMetadataService modelMetadataService;
		private readonly IStartInstanceService startInstanceService;
		private readonly IModelViewService modelViewService;
		private readonly ModelMetadata modelMetadata;


		internal MainWindowViewModel(
			ModelMetadata modelMetadata,
			ILatestVersionService latestVersionService,
			IMainWindowService mainWindowService,
			ModelViewModel modelViewModel,
			IOpenModelService openModelService,
			IRecentModelsService recentModelsService,
			IModelMetadataService modelMetadataService,
			IStartInstanceService startInstanceService,
			IModelViewService modelViewService)
		{
			this.modelMetadata = modelMetadata;

			this.mainWindowService = mainWindowService;
			this.openModelService = openModelService;
			this.recentModelsService = recentModelsService;
			this.modelMetadataService = modelMetadataService;
			this.startInstanceService = startInstanceService;
			this.modelViewService = modelViewService;

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
				var items = modelViewService.Search(value).Take(21).OrderBy(i => i.Name).ToList();
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


		void ClearSelectionItems()
		{
			SearchItems.Clear();
			SearchItems.Add(new SearchEntry("", null));
		}

		public BusyIndicator Busy => BusyIndicator();

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



		private IReadOnlyList<HiddenNodeItem> GetHiddenNodes()
		{
			return modelViewService.GetHiddenNodeNames()
				.Select(name => new HiddenNodeItem(name, modelViewService.ShowHiddenNode))
				.ToList();
		}


		public bool HasResent => recentModelsService.GetModelPaths().Any();
		public bool HasHiddenNodes => modelViewService.GetHiddenNodeNames().Any();

		public Command RefreshCommand => AsyncCommand(ManualRefreshAsync);
		public Command RefreshLayoutCommand => AsyncCommand(ManualRefreshLayoutAsync);

		public Command OpenFileCommand => Command(openModelService.ShowOpenModelDialog);


		public Command RunLatestVersionCommand => AsyncCommand(RunLatestVersionAsync);

		public Command FeedbackCommand => Command(mainWindowService.SendFeedback);

		public Command OptionsCommand => Command(mainWindowService.OpenOptions);

		public Command HelpCommand => Command(mainWindowService.OpenHelp);

		public Command MinimizeCommand => Command(Minimize);

		public Command CloseCommand => Command(CloseWindow);

		public Command ExitCommand => Command(Exit);

		public Command ToggleMaximizeCommand => Command(ToggleMaximize);

		public Command EscapeCommand => Command(Escape);

		public Command ClearFilterCommand => Command(ClearFilter);

		public Command SearchCommand => Command(Search);



		public async Task LoadAsync()
		{
			await Task.Yield();
		}


		public void ClosingWindow() => ModelViewModel.Close();

		private Task ManualRefreshAsync() => ModelViewModel.ManualRefreshAsync();

		private Task ManualRefreshLayoutAsync() => ModelViewModel.ManualRefreshAsync(true);

		private void Search() => mainWindowService.SetSearchFocus();


		public Task ActivateRefreshAsync()
		{
			return ModelViewModel.ActivateRefreshAsync();
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


		private static void CloseWindow() => Application.Current.Shutdown(0);

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