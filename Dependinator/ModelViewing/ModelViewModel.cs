using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Dependinator.ModelViewing.Items;
using Dependinator.ModelViewing.Private;
using Dependinator.Utils.Dependencies;
using Dependinator.Utils.UI.Mvvm;


namespace Dependinator.ModelViewing
{
	[SingleInstance]
	internal class ModelViewModel : ViewModel
	{
		private readonly IModelViewModelService modelViewModelService;


		private int width = 0;


		public ModelViewModel(IModelViewModelService modelViewModelService)
		{
			this.modelViewModelService = modelViewModelService;

			ItemsCanvas rootCanvas = new ItemsCanvas();
			ItemsViewModel = new ItemsViewModel(rootCanvas, null);

			modelViewModelService.SetRootCanvas(rootCanvas);
		}

		public ItemsViewModel ItemsViewModel { get; }


		public async Task OpenAsync() => await modelViewModelService.OpenAsync();


		public Task OpenFilesAsync(IReadOnlyList<string> filePaths) =>
			modelViewModelService.OpenFilesAsync(filePaths);


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