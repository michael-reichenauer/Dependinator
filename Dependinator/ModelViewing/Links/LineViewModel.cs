using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Dependinator.ModelHandling;
using Dependinator.ModelHandling.Core;
using Dependinator.ModelHandling.Private.Items;
using Dependinator.Utils;
using Dependinator.Utils.UI;

namespace Dependinator.ModelViewing.Links
{
	internal class LineViewModel : ItemViewModel
	{
		private readonly ILineViewModelService lineViewModelService;
		private readonly DelayDispatcher mouseOverDelay = new DelayDispatcher();
		private readonly Lazy<ObservableCollection<LinkItem>> sourceLinks;
		private readonly Lazy<ObservableCollection<LinkItem>> targetLinks;

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

			sourceLinks = new Lazy<ObservableCollection<LinkItem>>(GetSourceLinkItems);
			targetLinks = new Lazy<ObservableCollection<LinkItem>>(GetTargetLinkItems);
		}


		public override bool CanShow =>
			ItemScale < 40
			&& line.Source.CanShow && line.Target.CanShow;

		public double LineWidth => lineViewModelService.GetLineWidth(line);

		public double ArrowWidth => lineViewModelService.GetArrowWidth(line);

		public Brush LineBrush => line.Source != line.Target.Parent
			? line.Source.ViewModel.RectangleBrush
			: line.Target.ViewModel.RectangleBrush;

		public bool IsMouseOver { get => Get(); private set => Set(value); }
		public bool IsShowPoints { get => Get(); private set => Set(value); }

		public string LineData => lineViewModelService.GetLineData(line);

		public string PointsData => lineViewModelService.GetPointsData(line);

		public string ArrowData => lineViewModelService.GetArrowData(line);

		public string StrokeDash => "";

		public string ToolTip { get => Get(); private set => Set(value); }


		public void UpdateToolTip() => ToolTip = lineViewModelService.GetLineToolTip(line);

		public ObservableCollection<LinkItem> SourceLinks => sourceLinks.Value;

		public ObservableCollection<LinkItem> TargetLinks => targetLinks.Value;


		public void ToggleLine()
		{

		}



		private ObservableCollection<LinkItem> GetSourceLinkItems()
		{
			IEnumerable<LinkItem> items = lineViewModelService.GetSourceLinkItems(line);
			return new ObservableCollection<LinkItem>(items);
		}



		private ObservableCollection<LinkItem> GetTargetLinkItems()
		{
			IEnumerable<LinkItem> items = lineViewModelService.GetTargetLinkItems(line);
			return new ObservableCollection<LinkItem>(items);
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
				// First move event, lets start a move by  getting the index of point to move.
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
			IsShowPoints = true;
			Mouse.OverrideCursor = Cursors.Hand;
			NotifyAll();
		}


		public void ZoomLinks(double zoom, Point viewPosition)
		{
		}


		public void OnMouseEnter()
		{
			mouseOverDelay.Delay(ModelViewModel.MouseEnterDelay, _ =>
			{
				if (ModelViewModel.IsControlling)
				{
					Mouse.OverrideCursor = Cursors.Hand;
				}

				IsMouseOver = true;
				IsShowPoints = ModelViewModel.IsControlling;
				Notify(nameof(LineBrush), nameof(LineWidth), nameof(ArrowWidth));
			});
		}


		public void OnMouseLeave()
		{
			Mouse.OverrideCursor = null;
			mouseOverDelay.Cancel();
			IsMouseOver = false;
			IsShowPoints = false;
			Notify(nameof(LineBrush), nameof(LineWidth), nameof(ArrowWidth));
		}


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


		public void UpdateLine()
		{
			try
			{
				if (!CanShow)
				{
					return;
				}

				lineViewModelService.UpdateLineEndPoints(line);
				lineViewModelService.UpdateLineBounds(line);
			}
			catch (Exception e)
			{
				Log.Exception(e);
			}
		}



		private void TrackSourceOrTargetChanges()
		{
			try
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
			catch (Exception e)
			{
				Log.Exception(e);
			}
		}


		private void SourceOrTargetChanged(string propertyName)
		{
			UpdateLine();
			NotifyAll();
		}
	}
}