using System;
using System.Windows;
using System.Windows.Input;
using Dependinator.ModelViewing.DependencyExploring.Private;
using Dependinator.ModelViewing.Items;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.ModelViewing.ModelHandling.Private;


namespace Dependinator.ModelViewing.Lines.Private
{
	internal class LineViewModelService : ILineViewModelService
	{
		private readonly IModelService modelService;
		private readonly ILineControlService lineControlService;
		private readonly ILineDataService lineDataService;
		private readonly IItemSelectionService itemSelectionService;
		private readonly Func<Node, Line, DependencyExplorerWindow> dependencyExplorerWindowProvider;


		public LineViewModelService(
			IModelService modelService,
			ILineControlService lineControlService,
			ILineDataService lineDataService,
			IItemSelectionService itemSelectionService,
			Func<Node, Line, DependencyExplorerWindow> dependencyExplorerWindowProvider)
		{
			this.modelService = modelService;
			this.lineControlService = lineControlService;
			this.lineDataService = lineDataService;
			this.itemSelectionService = itemSelectionService;
			this.dependencyExplorerWindowProvider = dependencyExplorerWindowProvider;
		}


		public void UpdateLineBounds(Line line) => lineDataService.UpdateLineBounds(line);

		public double GetLineWidth(Line line) => lineDataService.GetLineWidth(line);


		public string GetLineData(Line line) => lineDataService.GetLineData(line);

		public string GetPointsData(Line line) => lineDataService.GetPointsData(line);


		public string GetArrowData(Line line) => lineDataService.GetArrowData(line);


		public double GetArrowWidth(Line line) => lineDataService.GetArrowWidth(line);


		public string GetEndPointsData(Line line) => lineDataService.GetEndPointsData(line);


		public void UpdateLineEndPoints(Line line) => lineDataService.UpdateLineEndPoints(line);


		public LineControl GetLineControl(Line line) => new LineControl(lineControlService, line);


		public void Clicked(LineViewModel lineViewModel) => itemSelectionService.Select(lineViewModel);


		public void OnMouseWheel(LineViewModel lineViewModel, UIElement uiElement, MouseWheelEventArgs e)
		{
			if (lineViewModel.Line.Owner.View.ViewModel != null)
			{
				lineViewModel.Line.Owner.View.ViewModel.OnMouseWheel(uiElement, e);
			}
			else
			{
				lineViewModel.Line.Owner.Root.View.ItemsCanvas.ZoomNode(e);
			}
		}


		public void ShowReferences(LineViewModel lineViewModel)
		{
			DependencyExplorerWindow window = dependencyExplorerWindowProvider(null, lineViewModel.Line);
			window.Show();
		}


		public void SetIsChanged() => modelService.SetIsChanged();
	}
}