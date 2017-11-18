using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Dependinator.Common.ThemeHandling;
using Dependinator.ModelViewing.Links;
using Dependinator.ModelViewing.Private;
using Dependinator.Utils;


namespace Dependinator.ModelViewing.Nodes.Private
{
	internal class NodeViewModelService : INodeViewModelService
	{
		private readonly IThemeService themeService;
		private readonly IModelLinkService modelLinkService;
		private readonly ILinkMenuItemService linkMenuItemService;


		public NodeViewModelService(
			IThemeService themeService,
			IModelLinkService modelLinkService,
			ILinkMenuItemService linkMenuItemService)
		{
			this.themeService = themeService;
			this.modelLinkService = modelLinkService;
			this.linkMenuItemService = linkMenuItemService;
		}



		public Brush GetNodeBrush(Node node)
		{
			return node.Color != null
				? Converter.BrushFromHex(node.Color)
				: GetRandomRectangleBrush(node.Name.DisplayName);
		}


		public void FirstShowNode(Node node)
		{
			node.SourceLines
				.Where(line => line.ViewModel == null)
				.ForEach(line => modelLinkService.AddLineViewModel(line));

			node.TargetLines
				.Where(line => line.ViewModel == null)
				.ForEach(line => modelLinkService.AddLineViewModel(line));
		}



		public IEnumerable<LinkItem> GetIncomingLinkItems(Node node)
		{
			IEnumerable<Line> lines = node.TargetLines
				.Where(line => line.Owner != node);

			return linkMenuItemService.GetSourceLinkItems(lines);
		}


		public IEnumerable<LinkItem> GetOutgoingLinkItems(Node node)
		{
			IEnumerable<Line> lines = node.SourceLines
				.Where(line => line.Owner != node);

			return linkMenuItemService.GetTargetLinkItems(lines);
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

		public Brush GetRectangleHighlightBrush(Brush brush)
		{
			return themeService.GetRectangleHighlighterBrush(brush);
		}


		public int GetPointIndex(Node node, Point point)
		{
			double scale = node.ViewModel.ItemScale;
			double dist = 15 / scale;
			NodeViewModel viewModel = node.ViewModel;

			if ((point - viewModel.ItemBounds.Location).Length < dist)
			{
				// Move left,top
				return 1;
			}
			else if ((point - new Point(
				viewModel.ItemLeft + viewModel.ItemWidth,
				viewModel.ItemTop)).Length < dist)
			{
				// Move right,top
				return 2;
			}
			else if ((point - new Point(
				viewModel.ItemLeft + viewModel.ItemWidth,
				viewModel.ItemTop + viewModel.ItemHeight)).Length < dist)
			{
				// Move right,bottom
				return 3;
			}
			else if ((point - new Point(
				viewModel.ItemLeft,
				viewModel.ItemTop + viewModel.ItemHeight)).Length < dist)
			{
				// Move left,bottom
				return 4;
			}

			Log.Debug("Move node");

			// Move node
			return 0;
		}


		public void MovePoint(Node node, int index, Point point, Point previousPoint)
		{
			NodeViewModel viewModel = node.ViewModel;

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

			double dist = 15 / scale;

			if (size.Width + resize.X < dist || size.Height + resize.Y < dist)
			{
				return;
			}

			Size newSiz = new Size(size.Width + resize.X, size.Height + resize.Y);
			viewModel.ItemBounds = new Rect(newLocation, newSiz);
			viewModel.ItemsViewModel?.MoveCanvas(offset);
			viewModel.ItemOwnerCanvas.UpdateItem(viewModel);
		}




	}
}
