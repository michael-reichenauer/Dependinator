using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Dependinator.Common.ProgressHandling;
using Dependinator.Common.ThemeHandling;
using Dependinator.ModelViewing.Private;
using Dependinator.ModelViewing.Private.Items;
using Dependinator.Utils;
using Dependinator.Utils.UI.Mvvm;


namespace Dependinator.ModelViewing
{
	[SingleInstance]
	internal class ModelViewModel : ViewModel
	{
		public static readonly TimeSpan MouseEnterDelay = TimeSpan.FromMilliseconds(100);

		public static bool IsControlling => Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
		
		private readonly IThemeService themeService;
		private readonly IModelViewService modelViewService;
		private readonly IProgressService progress;

		private int width = 0;


		public ModelViewModel(
			IModelViewService modelViewService,
			IThemeService themeService,
			IProgressService progressService)
		{
			this.modelViewService = modelViewService;
			this.themeService = themeService;
			this.progress = progressService;

			ItemsCanvas rootCanvas = new ItemsCanvas();
			ItemsViewModel = new ItemsViewModel(rootCanvas);

			modelViewService.SetRootCanvas(rootCanvas);
		}

		public ItemsViewModel ItemsViewModel { get; }

		
		public async Task LoadAsync()
		{
			await modelViewService.LoadAsync();
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
	}
}