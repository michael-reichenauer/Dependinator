using System;
using System.Windows;
using System.Windows.Media;
using Dependinator.ModelViewing.Private.Items;
using Dependinator.Utils;
using Dependinator.Utils.UI;

namespace Dependinator.ModelViewing.Links
{
	internal class LineViewModel : ItemViewModel
	{
		private static readonly TimeSpan MouseEnterDelay = TimeSpan.FromMilliseconds(100);

		private readonly ILineViewModelService lineViewModelService;
		private readonly DelayDispatcher mouseOverDelay = new DelayDispatcher();

		private readonly Line line;
		private Point mouseDownPoint;
		private int currentPointIndex = -1;


		public LineViewModel(ILineViewModelService lineViewModelService, Line line)
		{
			this.lineViewModelService = lineViewModelService;
			this.line = line;
			line.ViewModel = this;
			ItemZIndex = -1;

			UpdateLine();
			TrackSourceOrTargetChanges();
		}

		public override bool CanShow => line.Source.ViewModel.CanShow & line.Target.ViewModel.CanShow;

		public double LineWidth => lineViewModelService.GetLineWidth(line);

		public double ArrowWidth => lineViewModelService.GetArrowWidth(line);

		public Brush LineBrush => line.Source != line.Target.Parent
			? line.Source.ViewModel.RectangleBrush
			: line.Target.ViewModel.RectangleBrush;

		public bool IsMouseOver { get => Get(); private set => Set(value); }

		public string LineData => lineViewModelService.GetLineData(line);

		public string PointsData => lineViewModelService.GetPointsData(line);

		public string ArrowData => lineViewModelService.GetArrowData(line);

		public string StrokeDash => "";

		public string ToolTip => lineViewModelService.GetLineToolTip(line);



		public void ToggleLine()
		{

		}


		public void MouseDown(Point screenPoint)
		{
			mouseDownPoint = ItemOwnerCanvas.RootScreenToCanvasPoint(screenPoint);
			currentPointIndex = -1;
		}


		public void MouseUp(Point screenPoint)
		{
			if (currentPointIndex != -1)
			{
				EndMoveLinePoint();
			}
			else
			{
				Log.Debug("Mouse click");
			}
		}


		public void MouseMove(Point screenPoint)
		{
			Point point = ItemOwnerCanvas.RootScreenToCanvasPoint(screenPoint);

			if (currentPointIndex == -1)
			{
				// First move event, lets start a move by getting the index of point to move.
				// THis might create a new point if there is no existing point near the mouse down point
				currentPointIndex = lineViewModelService.GetLinePointIndex(line, mouseDownPoint);
				if (currentPointIndex == -1)
				{
					// Point not close enough to the line
					return;
				}
			}

			lineViewModelService.MoveLinePoint(line, currentPointIndex, point);
			lineViewModelService.UpdateLineBounds(line);
			IsMouseOver = true;
			NotifyAll();
		}


		public void ZoomLinks(double zoom, Point viewPosition)
		{
		}


		public void OnMouseEnter()
		{
			mouseOverDelay.Delay(MouseEnterDelay, _ =>
			{
				IsMouseOver = true;
				Notify(nameof(LineBrush), nameof(LineWidth), nameof(ArrowWidth));
			});
		}


		public void OnMouseLeave()
		{
			mouseOverDelay.Cancel();
			IsMouseOver = false;
			Notify(nameof(LineBrush), nameof(LineWidth), nameof(ArrowWidth));
		}

		public void UpdateToolTip() => Notify(nameof(ToolTip));


		private void EndMoveLinePoint()
		{
			if (currentPointIndex != line.FirstIndex && currentPointIndex != line.LastIndex)
			{
				// Removing the point if it is no longer needed (in the same line as neighbors points
				if (lineViewModelService.IsOnLineBetweenNeighbors(line, currentPointIndex))
				{
					line.Points.RemoveAt(currentPointIndex);
				}
			}

			lineViewModelService.UpdateLineBounds(line);
			NotifyAll();
			currentPointIndex = -1;
		}


		public override string ToString() => $"{line}";


		private void UpdateLine()
		{
			if (!CanShow)
			{
				return;
			}

			lineViewModelService.UpdateLineEndPoints(line);
			lineViewModelService.UpdateLineBounds(line);
		}


		private void TrackSourceOrTargetChanges()
		{
			if (line.Source == line.Target.Parent)
			{
				// Source node is parent of target, need to update line when source canvas is moved
				WhenSet(line.Source.ItemsCanvas, nameof(line.Source.ItemsCanvas.Offset))
					.Notify(SourceOrTargetChanged);

				// Update line when target node is moved
				WhenSet(line.Target.ViewModel, nameof(line.Target.ViewModel.ItemBounds))
					.Notify(SourceOrTargetChanged);

			}
			else if (line.Source.Parent == line.Target)
			{
				// Source node is child of target node, update line when target canvas is moved
				WhenSet(line.Target.ItemsCanvas, nameof(line.Target.ItemsCanvas.Offset))
					.Notify(SourceOrTargetChanged);

				// Update line when source node is moved
				WhenSet(line.Source.ViewModel, nameof(line.Source.ViewModel.ItemBounds))
					.Notify(SourceOrTargetChanged);
			}
			else
			{
				// Source and targets are siblings. update line when either node is moved
				WhenSet(line.Source.ViewModel, nameof(line.Source.ViewModel.ItemBounds))
					.Notify(SourceOrTargetChanged);
				WhenSet(line.Target.ViewModel, nameof(line.Target.ViewModel.ItemBounds))
					.Notify(SourceOrTargetChanged);
			}
		}


		private void SourceOrTargetChanged(string propertyName)
		{
			UpdateLine();
			NotifyAll();
		}
	}
}