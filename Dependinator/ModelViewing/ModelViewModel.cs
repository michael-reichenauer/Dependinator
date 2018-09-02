using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Dependinator.Common.ModelMetadataFolders;
using Dependinator.Common.ModelMetadataFolders.Private;
using Dependinator.ModelViewing.Private;
using Dependinator.ModelViewing.Private.ItemsViewing;
using Dependinator.Utils.Dependencies;
using Dependinator.Utils.UI.Mvvm;


namespace Dependinator.ModelViewing
{
    [SingleInstance]
    internal class ModelViewModel : ViewModel
    {
        private readonly IModelViewModelService modelViewModelService;
        private readonly ModelMetadata modelMetadata;


        private int width;


        public ModelViewModel(
            IModelViewModelService modelViewModelService,
            ModelMetadata modelMetadata)
        {
            this.modelViewModelService = modelViewModelService;
            this.modelMetadata = modelMetadata;

            ItemsCanvas rootCanvas = new ItemsCanvas();
            ItemsViewModel = new ItemsViewModel(rootCanvas);

            modelViewModelService.SetRootCanvas(rootCanvas);
        }


        public ItemsViewModel ItemsViewModel { get; }

        public Command AddNodeCommand => Command(modelViewModelService.AddNewNode);


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


        public async Task OpenAsync()
        {
            if (modelMetadata.IsDefault)
            {
                // Model not needed for open model view
                return;
            }

            await modelViewModelService.OpenAsync();
        }


        public Task OpenFilesAsync(IReadOnlyList<string> filePaths) =>
            modelViewModelService.OpenFilesAsync(filePaths);


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
