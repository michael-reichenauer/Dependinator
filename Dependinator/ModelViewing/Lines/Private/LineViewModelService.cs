using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using Dependinator.Common;
using Dependinator.ModelViewing.DependencyExploring;
using Dependinator.ModelViewing.DependencyExploring.Private;
using Dependinator.ModelViewing.Items;
using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing.Lines.Private
{
	internal class LineViewModelService : ILineViewModelService
	{
		private readonly ILineMenuItemService lineMenuItemService;
		private readonly ILineControlService lineControlService;
		private readonly ILineZoomService lineZoomService;
		private readonly ILineDataService lineDataService;
		private readonly IItemSelectionService itemSelectionService;
		private readonly IDependenciesService dependenciesService;

		private readonly WindowOwner owner;


		public LineViewModelService(
			ILineMenuItemService lineMenuItemService,
			ILineControlService lineControlService,
			ILineZoomService lineZoomService,
			ILineDataService lineDataService,
			IItemSelectionService itemSelectionService,
			IDependenciesService dependenciesService,
			WindowOwner owner)
		{
			this.lineMenuItemService = lineMenuItemService;
			this.lineControlService = lineControlService;
			this.lineZoomService = lineZoomService;
			this.lineDataService = lineDataService;
			this.itemSelectionService = itemSelectionService;
			this.dependenciesService = dependenciesService;
			this.owner = owner;
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


		public IEnumerable<LineMenuItemViewModel> GetTargetLinkItems(Line line) =>
			lineMenuItemService.GetTargetLinkItems(line);


		public IEnumerable<LineMenuItemViewModel> GetSourceLinkItems(Line line) =>
			lineMenuItemService.GetSourceLinkItems(line);


		public void Clicked(LineViewModel lineViewModel) => itemSelectionService.Select(lineViewModel);


		public void OnMouseWheel(LineViewModel lineViewModel, UIElement uiElement, MouseWheelEventArgs e)
		{
			if (lineViewModel.Line.Owner.View.ViewModel != null)
			{
				lineViewModel.Line.Owner.View.ViewModel.OnMouseWheel(uiElement, e);
			}
			else
			{
				lineViewModel.Line.Owner.Root.View.ItemsCanvas.OnMouseWheel(uiElement, e, false);
			}
		}


		public void Toggle(Line line)
		{
			lineZoomService.ZoomInLinkLine(line);
		}


		public void ShowReferences(LineViewModel lineViewModel)
		{
			Line line = lineViewModel.Line;

			DependencyExplorerWindow dependencyExplorerWindow = new DependencyExplorerWindow(dependenciesService, owner, line);
			dependencyExplorerWindow.Show();
		}
	}
}