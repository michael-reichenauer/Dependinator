﻿using System;
using System.Windows;
using System.Windows.Input;
using Dependinator.ModelViewing.Private.DependencyExploring;
using Dependinator.ModelViewing.Private.ModelHandling.Core;
using Dependinator.ModelViewing.Private.ModelHandling.Private;


namespace Dependinator.ModelViewing.Private.Lines.Private
{
    internal class LineViewModelService : ILineViewModelService
    {
        private readonly Func<Node, Line, DependencyExplorerWindow> dependencyExplorerWindowProvider;
        private readonly ILineControlService lineControlService;
        private readonly ILineDataService lineDataService;
        private readonly IModelDatabase modelService;
        private readonly ISelectionService selectionService;


        public LineViewModelService(
            IModelDatabase modelService,
            ILineControlService lineControlService,
            ILineDataService lineDataService,
            ISelectionService selectionService,
            Func<Node, Line, DependencyExplorerWindow> dependencyExplorerWindowProvider)
        {
            this.modelService = modelService;
            this.lineControlService = lineControlService;
            this.lineDataService = lineDataService;
            this.selectionService = selectionService;
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


        public void Clicked(LineViewModel lineViewModel) => selectionService.Select(lineViewModel);


        public void OnMouseWheel(LineViewModel lineViewModel, UIElement uiElement, MouseWheelEventArgs e)
        {
            if (lineViewModel.Line.Owner.ViewModel != null)
            {
                lineViewModel.Line.Owner.ViewModel.OnMouseWheel(uiElement, e);
            }
            else
            {
                lineViewModel.Line.Owner.Root.ItemsCanvas.Zoom(e);
            }
        }


        public void ShowReferences(LineViewModel lineViewModel)
        {
            DependencyExplorerWindow window = dependencyExplorerWindowProvider(null, lineViewModel.Line);
            window.Show();
        }


        public void SetIsChanged(LineViewModel lineViewModel) => modelService.SetIsChanged(lineViewModel.Line);
    }
}
