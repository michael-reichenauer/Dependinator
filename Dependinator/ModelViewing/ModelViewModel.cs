using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Dependinator.Common.ModelMetadataFolders;
using Dependinator.Common.ProgressHandling;
using Dependinator.Common.ThemeHandling;
using Dependinator.ModelViewing.Items;
using Dependinator.ModelViewing.Nodes.Private;
using Dependinator.Utils;
using Dependinator.Utils.UI.Mvvm;


namespace Dependinator.ModelViewing
{
	[SingleInstance]
	internal class ModelViewModel : ViewModel
	{
		public static readonly TimeSpan MouseEnterDelay = TimeSpan.FromMilliseconds(100);
		public static readonly TimeSpan MouseExitDelay = TimeSpan.FromMilliseconds(10);

		public static bool IsControlling => Keyboard.Modifiers.HasFlag(ModifierKeys.Control);

		private readonly IThemeService themeService;
		private readonly IModelViewService modelViewService;
		private readonly IProgressService progress;
		private readonly IOpenModelService openModelService;
		private readonly ModelMetadata modelMetadata;

		private int width = 0;


		public ModelViewModel(
			IModelViewService modelViewService,
			IThemeService themeService,
			IProgressService progressService,
			IOpenModelService openModelService,
			INodeSelectionService nodeSelectionService,
			ModelMetadata modelMetadata)
		{
			this.modelViewService = modelViewService;
			this.themeService = themeService;
			this.progress = progressService;
			this.openModelService = openModelService;
			this.modelMetadata = modelMetadata;

			ItemsCanvas rootCanvas = new ItemsCanvas();
			ItemsViewModel = new ItemsViewModel(nodeSelectionService, rootCanvas, null);

			modelViewService.SetRootCanvas(rootCanvas);
		}

		public ItemsViewModel ItemsViewModel { get; }


		public async Task LoadAsync()
		{
			await openModelService.OpenOtherModelAsync(modelMetadata.ModelFilePath);
			//await modelViewService.LoadAsync();
		}


		public int Width
		{
			get => width;
			set
			{
				if (width != value)
				{
					width = value;
					ItemsViewModel.SizeChanged();
				}
			}
		}


		public async Task ActivateRefreshAsync()
		{
			themeService.SetThemeWpfColors();

			using (progress.ShowBusy())
			{
				await Task.Yield();
			}
		}


		public async Task ManualRefreshAsync(bool refreshLayout = false)
		{
			using (progress.ShowBusy())
			{
				await modelViewService.RefreshAsync(refreshLayout);
			}
		}


		public void Close() => modelViewService.Close();


		public Task LoadFilesAsync(IReadOnlyList<string> filePaths)
		{
			return openModelService.OpenModelAsync(filePaths);
		}


		public void MouseClicked(MouseButtonEventArgs mouseButtonEventArgs)
		{
			modelViewService.Clicked();
		}


		public void OnMouseWheel(UIElement uiElement, MouseWheelEventArgs e)
		{
			modelViewService.OnMouseWheel(uiElement, e);
		}
	}
}