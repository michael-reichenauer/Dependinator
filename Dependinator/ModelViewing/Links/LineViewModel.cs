using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.ModelViewing.ModelHandling.Private.Items;
using Dependinator.Utils;
using Dependinator.Utils.UI;

namespace Dependinator.ModelViewing.Links
{
	internal class LineViewModel : ItemViewModel
	{
		private readonly ILineViewModelService lineViewModelService;
		private readonly DelayDispatcher delayDispatcher = new DelayDispatcher();
		private readonly Lazy<ObservableCollection<LinkItem>> sourceLinks;
		private readonly Lazy<ObservableCollection<LinkItem>> targetLinks;
		private static TimeSpan MouseExitDelay => TimeSpan.FromMilliseconds(10);

		private readonly Line line;
		private Point mouseDownPoint;
		private int currentPointIndex = -1;


		public LineViewModel(ILineViewModelService lineViewModelService, Line line)
		{
			this.lineViewModelService = lineViewModelService;
			this.line = line;
			line.View.ViewModel = this;
			ItemZIndex = -1;

			UpdateLine();
			TrackSourceOrTargetChanges();

			sourceLinks = new Lazy<ObservableCollection<LinkItem>>(GetSourceLinkItems);
			targetLinks = new Lazy<ObservableCollection<LinkItem>>(GetTargetLinkItems);
		}


		public override bool CanShow =>
			ItemScale < 40
			&& line.Source.View.CanShow && line.Target.View.CanShow;

		public double LineWidth => lineViewModelService.GetLineWidth(line);

		public double ArrowWidth => lineViewModelService.GetArrowWidth(line);

		public Brush LineBrush => line.Source != line.Target.Parent
			? line.Source.View.ViewModel.RectangleBrush
			: line.Target.View.ViewModel.RectangleBrush;

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


		public override void MoveItem(Vector moveOffset)
		{
			for (int i = 1; i < line.View.Points.Count - 1; i++)
			{
				line.View.Points[i] = line.View.Points[i] + moveOffset;
			}
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

		public void MouseDown(Point screenPoint, bool isPoint)
		{
			isPointMove = isPoint;
			mouseDownPoint = ItemOwnerCanvas.RootScreenToCanvasPoint(screenPoint);
			currentPointIndex = -1;
			IsMouseOver = true;
			IsShowPoints = true;
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
				currentPointIndex = lineViewModelService.GetLinePointIndex(
					line, mouseDownPoint, isPointMove);
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


		private bool isPointMove = false;


		public void OnMouseEnter(bool isPoint)
		{
			isPointMove = isPoint;

			if (isPoint)
			{
				Mouse.OverrideCursor = Cursors.SizeAll;
				IsMouseOver = true;
				IsShowPoints = true;
				Notify(nameof(LineBrush), nameof(LineWidth), nameof(ArrowWidth));
			}
			else
			{
				delayDispatcher.Delay(ModelViewModel.MouseEnterDelay, _ =>
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
		}


		public void OnMouseLeave(bool isPoint)
		{
			if (!IsShowPoints)
			{
				Mouse.OverrideCursor = null;
				delayDispatcher.Cancel();
				IsMouseOver = false;
				IsShowPoints = false;
				isPointMove = false;
				Notify(nameof(LineBrush), nameof(LineWidth), nameof(ArrowWidth));
			}
			else
			{
				if (isPoint)
				{
					IsShowPoints = false;
					OnMouseLeave(false);
				}
				else
				{
					delayDispatcher.Delay(MouseExitDelay, _ =>
					{
						if (!isPointMove)
						{
							IsShowPoints = false;
							OnMouseLeave(false);
						}
					});
				}
			}
		}


		private void EndMoveLinePoint()
		{
			if (currentPointIndex != line.View.FirstIndex && currentPointIndex != line.View.LastIndex)
			{
				// Removing the point if it is no longer needed (in the same line as neighbors points
				if (lineViewModelService.IsOnLineBetweenNeighbors(line, currentPointIndex))
				{
					line.View.Points.RemoveAt(currentPointIndex);
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
				//if (line.Source == line.Target.Parent)
				//{
				//	// Source node is parent of target, need to update line when source canvas is moved
				//	//WhenSet(line.Source.ItemsCanvas, nameof(line.Source.ItemsCanvas.Offset))
				//	//	.Notify(SourceOrTargetChanged);

				//	// Update line when target node is moved
				//	WhenSet(line.Target.ViewModel, nameof(line.Target.ViewModel.ItemBounds))
				//		.Notify(SourceOrTargetChanged);

				//}
				//else if (line.Source.Parent == line.Target)
				//{
				//	// Source node is child of target node, update line when target canvas is moved
				//	//WhenSet(line.Target.ItemsCanvas, nameof(line.Target.ItemsCanvas.Offset))
				//	//	.Notify(SourceOrTargetChanged);

				//	// Update line when source node is moved
				//	WhenSet(line.Source.ViewModel, nameof(line.Source.ViewModel.ItemBounds))
				//		.Notify(SourceOrTargetChanged);
				//}
				//else
				{
					// Source and targets are siblings. update line when either node is moved
					WhenSet(line.Source.View.ViewModel, nameof(line.Source.View.ViewModel.ItemBounds))
						.Notify(SourceOrTargetChanged);
					WhenSet(line.Target.View.ViewModel, nameof(line.Target.View.ViewModel.ItemBounds))
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