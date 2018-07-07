using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Dependinator.Common.ModelMetadataFolders;
using Dependinator.Common.ProgressHandling;
using Dependinator.Common.ThemeHandling;
using Dependinator.ModelViewing.Items;
using Dependinator.Utils.Dependencies;
using Dependinator.Utils.UI.Mvvm;


namespace Dependinator.ModelViewing
{
	[SingleInstance]
	internal class ModelViewModel : ViewModel
	{
		public static readonly TimeSpan MouseEnterDelay = TimeSpan.FromMilliseconds(100);

		private readonly IThemeService themeService;
		private readonly IModelViewModelService modelViewModelService;
		private readonly IProgressService progress;
		private readonly IOpenModelService openModelService;


		private int width = 0;


		public ModelViewModel(
			IModelViewModelService modelViewModelService,
			IThemeService themeService,
			IProgressService progressService,
			IOpenModelService openModelService)
		{
			this.modelViewModelService = modelViewModelService;
			this.themeService = themeService;
			this.progress = progressService;
			this.openModelService = openModelService;

			ItemsCanvas rootCanvas = new ItemsCanvas();
			ItemsViewModel = new ItemsViewModel(rootCanvas, null);

			modelViewModelService.SetRootCanvas(rootCanvas);
		}

		public ItemsViewModel ItemsViewModel { get; }


		public async Task LoadAsync() => await openModelService.OpenCurrentModelAsync();


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

			await Task.Yield();
		}


		public async Task ManualRefreshAsync(bool refreshLayout = false)
		{
			using (progress.ShowBusy())
			{
				await modelViewModelService.RefreshAsync(refreshLayout);
			}
		}


		public void Close() => modelViewModelService.Close();


		public Task LoadFilesAsync(IReadOnlyList<string> filePaths)
		{
			return openModelService.OpenModelAsync(filePaths);
		}


		public void MouseClicked(MouseButtonEventArgs mouseButtonEventArgs)
		{
			modelViewModelService.Clicked();
		}


		public void OnMouseWheel(UIElement uiElement, MouseWheelEventArgs e)
		{
			modelViewModelService.OnMouseWheel(uiElement, e);
		}
	}
}