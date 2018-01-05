using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Dependinator.Common.ThemeHandling;
using Dependinator.ModelViewing.Items;
using Dependinator.ModelViewing.Lines.Private;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.ModelViewing.ModelHandling.Private;
using Dependinator.Utils.UI;


namespace Dependinator.ModelViewing.Nodes.Private
{
	internal class NodeViewModelService : INodeViewModelService
	{
		private readonly IThemeService themeService;
		private readonly IModelLinkService modelLinkService;
		private readonly ILineMenuItemService lineMenuItemService;
		private readonly IGeometryService geometryService;
		private readonly IItemSelectionService itemSelectionService;


		public NodeViewModelService(
			IThemeService themeService,
			IModelLinkService modelLinkService,
			ILineMenuItemService lineMenuItemService,
			IGeometryService geometryService,
			IItemSelectionService itemSelectionService)
		{
			this.themeService = themeService;
			this.modelLinkService = modelLinkService;
			this.lineMenuItemService = lineMenuItemService;
			this.geometryService = geometryService;
			this.itemSelectionService = itemSelectionService;
		}



		public Brush GetNodeBrush(Node node)
		{
			return node.View.Color != null
				? Converter.BrushFromHex(node.View.Color)
				: GetRandomRectangleBrush(node.Name.DisplayName);
		}


		public void FirstShowNode(Node node)
		{
			node.SourceLines
				.Where(line => line.View.ViewModel == null)
				.ForEach(line => modelLinkService.AddLineViewModel(line));

			node.TargetLines
				.Where(line => line.View.ViewModel == null)
				.ForEach(line => modelLinkService.AddLineViewModel(line));
		}



		public IEnumerable<LineMenuItemViewModel> GetIncomingLinkItems(Node node)
		{
			IEnumerable<Line> lines = node.TargetLines
				.Where(line => line.Owner != node);

			return lineMenuItemService.GetSourceLinkItems(lines);
		}


		public IEnumerable<LineMenuItemViewModel> GetOutgoingLinkItems(Node node)
		{
			IEnumerable<Line> lines = node.SourceLines
				.Where(line => line.Owner != node);

			return lineMenuItemService.GetTargetLinkItems(lines);
		}


		public void MouseClicked(NodeViewModel nodeViewModel)
		{
			itemSelectionService.Select(nodeViewModel);
		}


		public void OnMouseWheel(
			NodeViewModel nodeViewModel, 
			UIElement uiElement, 
			MouseWheelEventArgs e)
		{
			ItemsCanvas itemsCanvas = nodeViewModel.ItemsViewModel?.ItemsCanvas ?? nodeViewModel.Node.Root.View.ItemsCanvas;

			itemsCanvas.OnMouseWheel(uiElement, e, nodeViewModel.IsInnerSelected);
		}


		public Brush GetRandomRectangleBrush(string nodeName)
		{
			return themeService.GetRectangleBrush(nodeName);
		}

		public Brush GetBrushFromHex(string hexColor)
		{
			return themeService.GetBrushFromHex(hexColor);
		}

		public string GetHexColorFromBrush(Brush brush)
		{
			return themeService.GetHexColorFromBrush(brush);
		}

		public Brush GetBackgroundBrush(Brush brush)
		{
			return themeService.GetRectangleBackgroundBrush(brush);
		}

		public Brush GetSelectedBrush(Brush brush)
		{
			return themeService.GetRectangleSelectedBackgroundBrush(brush);
		}


		public Brush GetRectangleHighlightBrush(Brush brush)
		{
			return themeService.GetRectangleHighlighterBrush(brush);
		}


		public int GetPointIndex(Node node, Point point)
		{
			// Sometimes the point is a little bit off, lets adjust the point to be on the border
			Point pointInPerimeter = geometryService.GetPointInPerimeter(node.View.Bounds, point);

			Vector vector = point - pointInPerimeter;
			//Log.Debug($"Dist {vector.Length}, vector: {vector.TS()}");

			point = pointInPerimeter;

			double scale = node.View.ViewModel.ItemScale;
			double dist = Math.Max(40 / scale, 40);
			NodeViewModel viewModel = node.View.ViewModel;

			double ltf = (point - viewModel.ItemBounds.Location).Length;
			double ltr = (point - new Point(
				viewModel.ItemLeft + viewModel.ItemWidth, viewModel.ItemTop)).Length;

			double lbr = (point - new Point(
				viewModel.ItemLeft + viewModel.ItemWidth,
				viewModel.ItemTop + viewModel.ItemHeight)).Length;
			double lbl = (point - new Point(
				viewModel.ItemLeft, viewModel.ItemTop + viewModel.ItemHeight)).Length;


			//Log.Debug($"{ltf},{ltr}, {lbr},{lbl}");
			//int index = 0;
			//double minDist = double.MaxValue;

			//if (ltf < minDist)
			//{
			//	// Move left,top
			//	index = 1;
			//	minDist = ltf;
			//	Mouse.OverrideCursor = Cursors.SizeNWSE;
			//}

			//if (ltr < minDist)
			//{
			//	// Move right,top
			//	index = 2;
			//	minDist = ltr;
			//	Mouse.OverrideCursor = Cursors.SizeNESW;
			//}

			//if (lbr < minDist)
			//{
			//	// Move right,bottom
			//	index = 3;
			//	minDist = lbr;
			//	Mouse.OverrideCursor = Cursors.SizeNWSE;
			//}

			//if (lbl < minDist)
			//{
			//	// Move left,bottom
			//	index = 4;
			//	minDist = lbl;
			//	Mouse.OverrideCursor = Cursors.SizeNESW;
			//}

			//// Log.Warn("Move node");

			//if (minDist < 100)
			//{
			//	return index;
			//}

			// Move node
			Mouse.OverrideCursor = Cursors.Hand;
			return 0;
		}


		public bool MovePoint(Node node, int index, Point point, Point previousPoint)
		{
			NodeViewModel viewModel = node.View.ViewModel;

			Point location = viewModel.ItemBounds.Location;
			Point newLocation = location;
			double scale = viewModel.ItemScale;

			Size size = viewModel.ItemBounds.Size;
			Vector resize = new Vector(0, 0);
			Vector offset = new Vector(0, 0);

			if (index == 0)
			{
				Vector moved = point - previousPoint;
				newLocation = location + moved;
			}
			else if (index == 1)
			{
				newLocation = new Point(point.X, point.Y);
				resize = new Vector(location.X - newLocation.X, location.Y - newLocation.Y);
				offset = new Vector((location.X - newLocation.X) * scale, (location.Y - newLocation.Y) * scale);
			}
			else if (index == 2)
			{
				newLocation = new Point(location.X, point.Y);
				resize = new Vector((point.X - size.Width) - location.X, location.Y - newLocation.Y); ;
				offset = new Vector(0, (location.Y - newLocation.Y) * scale);
			}
			else if (index == 3)
			{
				newLocation = location;
				resize = new Vector((point.X - size.Width) - location.X, (point.Y - size.Height) - location.Y);
			}
			else if (index == 4)
			{
				newLocation = new Point(point.X, location.Y);
				resize = new Vector(location.X - newLocation.X, (point.Y - size.Height) - location.Y);
				offset = new Vector((location.X - newLocation.X) * scale, 0);
			}

			double dist = 50;

			double width = size.Width + resize.X;
			double height = size.Height + resize.Y;

			if (width < dist || height < dist)
			{
				return false;
			}

			//newLocation = new Point(newLocation.X.Rnd(5), newLocation.Y.Rnd(5));
			Size newSiz = new Size(width, height);

			if (newLocation.Same(viewModel.ItemBounds.Location)
					&& newSiz.Same(viewModel.ItemBounds.Size))
			{
				return false;
			}


			viewModel.ItemBounds = new Rect(newLocation, newSiz);
			viewModel.NotifyAll();
			viewModel.ItemsViewModel?.MoveCanvas(offset);
			viewModel.ItemOwnerCanvas.UpdateItem(viewModel);

			return true;
		}
	}
}
