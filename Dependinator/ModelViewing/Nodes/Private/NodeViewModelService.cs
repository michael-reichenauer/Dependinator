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
		private static readonly string MoreText = "...";
		private static readonly int LinksMenuLimit = 35;

		private static readonly Size DefaultSize = new Size(200, 100);
		//private static readonly int RowLength = 4;
		//private static readonly int Padding = 100;
		private static readonly double XMargin = 150;
		private static readonly double YMargin = 110;

		private Layout rootLayout = new Layout(0, new Size(1, 1), 1, 100, 1);

		private readonly Layout[] layouts =
		{
			new Layout(4, new Size(2.5, 2.5), 2, 100, 1),
			new Layout(9, new Size(1.5, 1.5), 3, 100, 1),
			new Layout(int.MaxValue, new Size(1, 1), 4, 100, 1),
		};
		

		private static readonly IEnumerable<LinkItem> EmptySubLinks = Enumerable.Empty<LinkItem>();


		private readonly IThemeService themeService;
		private readonly IModelLinkService modelLinkService;
		private readonly Model model;


		public NodeViewModelService(
			IThemeService themeService,
			IModelLinkService modelLinkService,
			Model model)
		{
			this.themeService = themeService;
			this.modelLinkService = modelLinkService;
			this.model = model;
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

		public void SetLayout(NodeViewModel nodeViewMode)
		{
			if (nodeViewMode.Node.Bounds != RectEx.Zero)
			{
				nodeViewMode.ItemBounds = nodeViewMode.Node.Bounds;
				return;
			}


			Node parentNode = nodeViewMode.Node.Parent;
			int i = 0;
			int count = parentNode.Children.Count;

			Layout layout = parentNode.IsRoot ? rootLayout : layouts.First(l => count <= l.Count);


			foreach (Node childNode in parentNode.Children)
			{
				int index = i++;

				while (true)
				{
					int siblingIndex = index++;

					Size parentSize = parentNode.IsRoot ? DefaultSize : parentNode.ViewModel.ItemBounds.Size;

					double scaleFactor = parentNode.ItemsCanvas.ScaleFactor;

					Size size = new Size(
						layout.Size.Width * parentSize.Width ,
						layout.Size.Height * parentSize.Height );

					double x = (siblingIndex % layout.RowLength) * (size.Width + layout.Padding) + XMargin;
					double y = (siblingIndex / layout.RowLength) * (size.Height + layout.Padding) + YMargin;
					Point location = new Point(x, y);

					Rect newBounds = new Rect(location, size);

					Rect currentBounds = childNode.ViewModel.ItemBounds;

					if (Math.Abs(newBounds.X - currentBounds.X) < 0.0001
						&& Math.Abs(newBounds.Y - currentBounds.Y) < 0.001
						&& Math.Abs(newBounds.Width - currentBounds.Width) < 0.001
						&& Math.Abs(newBounds.Height - currentBounds.Height) < 0.001)
					{
						break;
					}

					if (!parentNode.Children
						.Any(child => childNode != child && child.ViewModel.ItemBounds.IntersectsWith(newBounds)))
					{
						childNode.ViewModel.ItemBounds = newBounds;
						break;
					}
				}
			}


			//if (IsParentShowing(nodeViewMode))
			//{
			//	Log.Warn($"Showing {nodeViewMode.Node.Parent} when adding {nodeViewMode.Node} ");
			//}
			//else
			//{
			//	Log.Debug($"Not shown {nodeViewMode.Node.Parent} when adding {nodeViewMode.Node} ");
			//}


			//Node parentNode = nodeViewMode.Node.Parent;
			//int i = 0;
			//int count = parentNode.Children.Count;

			//Layout layout = parentNode.IsRoot ? rootLayout : layouts.First(l => count <= l.Count);


			//foreach (Node childNode in parentNode.Children)
			//{
			//	int index = i++;

			//	while (true)
			//	{
			//		int siblingIndex = index++;

			//		Size parentSize = parentNode.IsRoot ? DefaultSize : parentNode.ViewModel.ItemBounds.Size;

			//		double scaleFactor = parentNode.ItemsCanvas.ScaleFactor;

			//		Size size = DefaultSize;

			//		double x = (siblingIndex % layout.RowLength) * (size.Width + layout.Padding) + XMargin;
			//		double y = (siblingIndex / layout.RowLength) * (size.Height + layout.Padding) + YMargin;
			//		Point location = new Point(x, y);

			//		Rect newBounds = new Rect(location, size);

			//		Rect currentBounds = childNode.ViewModel.ItemBounds;

			//		//if (parentNode.ItemsCanvas.ScaleFactor != layout.ScaleFactor)

			//		if (Math.Abs(newBounds.X - currentBounds.X) < 0.0001
			//			&& Math.Abs(newBounds.Y - currentBounds.Y) < 0.001
			//			&& Math.Abs(newBounds.Width - currentBounds.Width) < 0.001
			//			&& Math.Abs(newBounds.Height - currentBounds.Height) < 0.001)
			//		{
			//			break;
			//		}

			//		if (!parentNode.Children
			//			.Any(child => childNode != child && child.ViewModel.ItemBounds.IntersectsWith(newBounds)))
			//		{
			//			childNode.ViewModel.ItemBounds = newBounds;
			//			break;
			//		}
			//	}
			//}


			//Node parentNode = nodeViewMode.Node.Parent;
			//int i = 0;
			//int count = parentNode.Children.Count;

			//Layout layout = parentNode.IsRoot ? rootLayout : layouts.First(l => count <= l.Count);


			//foreach (Node childNode in parentNode.Children)
			//{
			//	int index = i++;

			//	while (true)
			//	{
			//		int siblingIndex = index++;

			//		Size parentSize = parentNode.IsRoot ? DefaultSize : parentNode.ViewModel.ItemBounds.Size;

			//		double scaleFactor = parentNode.ItemsCanvas.ScaleFactor;

			//		Size size = new Size(
			//			layout.Size.Width * parentSize.Width * scaleFactor,
			//			layout.Size.Height * parentSize.Height * scaleFactor);

			//		double x = (siblingIndex % layout.RowLength) * (size.Width + layout.Padding) + XMargin;
			//		double y = (siblingIndex / layout.RowLength) * (size.Height + layout.Padding) + YMargin;
			//		Point location = new Point(x, y);

			//		Rect newBounds = new Rect(location, size);

			//		Rect currentBounds = childNode.ViewModel.ItemBounds;

			//		if (Math.Abs(newBounds.X - currentBounds.X) < 0.0001 
			//			&& Math.Abs(newBounds.Y - currentBounds.Y) < 0.001
			//			&& Math.Abs(newBounds.Width - currentBounds.Width) < 0.001
			//			&& Math.Abs(newBounds.Height - currentBounds.Height) < 0.001)
			//		{
			//			break;
			//		}

			//		if (!parentNode.Children
			//			.Any(child => childNode != child && child.ViewModel.ItemBounds.IntersectsWith(newBounds)))
			//		{
			//			childNode.ViewModel.ItemBounds = newBounds;
			//			break;
			//		}
			//	}
			//}


			//int index = nodeViewMode.Node.Parent.Children.Count - 1;
			//while (true)
			//{
			//	int siblingCount = index++;

			//	double x = (siblingCount % rowLength) * (DefaultSize.Width + padding) + xMargin;
			//	double y = (siblingCount / rowLength) * (DefaultSize.Height + padding) + yMargin;
			//	Point location = new Point(x, y);

			//	Rect bounds = new Rect(location, DefaultSize);

			//	if (!nodeViewMode.Node.Parent.Children.Any(child => child.ViewModel.ItemBounds.IntersectsWith(bounds)))
			//	{
			//		nodeViewMode.ItemBounds = bounds;
			//		return;
			//	}
			//}
		}


		private static bool IsParentShowing(NodeViewModel nodeViewMode)
		{
			return nodeViewMode.Node.IsRoot 
				|| nodeViewMode.Node.Parent.ViewModel.IsShowing;
		}


		public void ResetLayout(NodeViewModel nodeViewMode)
		{
			//int siblingCount = nodeViewMode.Node.Parent.Children.IndexOf(nodeViewMode.Node);

			//double x = (siblingCount % RowLength) * (DefaultSize.Width + Padding) + XMargin;
			//double y = (siblingCount / RowLength) * (DefaultSize.Height + Padding) + YMargin;
			//Point location = new Point(x, y);

			//Rect bounds = new Rect(location, DefaultSize);

			//nodeViewMode.ItemBounds = bounds;
			//nodeViewMode.ItemsViewModel?.ItemsCanvas?.ResetLayout();
		}


		public IEnumerable<LinkItem> GetLinkItems(
			IEnumerable<Line> lines, Func<Line, Node> lineEndPoint, Func<Link, Node> linkEndPoint)
		{
			Log.Debug($"Get items ...");
			
			List<LinkItem> lineItems = new List<LinkItem>();
			foreach (Line line in lines)
			{
				IEnumerable<Link> lineLinks = line.Links.DistinctBy(linkEndPoint);
				IEnumerable<LinkItem> linkItems = GetLinkItems(lineLinks, linkEndPoint, 1);
				lineItems.Add(new LinkItem(null, lineEndPoint(line).Name.DisplayName, linkItems));
			}

			return lineItems;
		}


		public IEnumerable<LinkItem> GetLinkItems(
			IEnumerable<Link> links, Func<Link, Node> endPoint, int level)
		{
			if (!links.Any())
			{
				return new List<LinkItem>();
			}
			else if (links.Count() < LinksMenuLimit)
			{
				return links.Select(link => new LinkItem(
					link, endPoint(link).Name.DisplayFullName, EmptySubLinks));
			}

			var groups = links
				.GroupBy(link => GetGroupKey(endPoint(link), level))
				.OrderBy(group => group.Key);

			List<LinkItem> linkItems = GetLinksGroupsItems(groups, endPoint, level);

			ReduceHierarchy(linkItems);

			linkItems.Sort(Comparer<LinkItem>.Create(
				(i1, i2) => i1.Text == MoreText
				? 1 : i2.Text == MoreText ? -1 : i1.Text.CompareTo(i2.Text)));

			return linkItems;
		}


		private static void ReduceHierarchy(ICollection<LinkItem> linkItems)
		{
			int margin = Math.Max(LinksMenuLimit - linkItems.Count, 0);

			while (margin >= 0)
			{
				bool isChanged = false;

				foreach (LinkItem linkItem in linkItems.Where(item => item.SubLinkItems.Any()).ToList())
				{
					int count = linkItem.SubLinkItems.Count();

					if (count > 4)
					{
						continue;
					}

					margin -= (count - 1);

					if (margin >= 0)
					{
						linkItems.Remove(linkItem);
						linkItem.SubLinkItems.ForEach(linkItems.Add);
						isChanged = true;
					}
				}

				if (!isChanged)
				{
					break;
				}
			}
		}


		private List<LinkItem> GetLinksGroupsItems(
			IEnumerable<IGrouping<string, Link>> linksGroups, Func<Link, Node> endPoint, int level)
		{
			var linkItems = linksGroups
				.Take(LinksMenuLimit)
				.Select(group => GetLinkItem(group, endPoint, level));

			if (linksGroups.Count() > LinksMenuLimit)
			{
				List<LinkItem> moreLinks = GetLinksGroupsItems(linksGroups.Skip(LinksMenuLimit), endPoint, level);

				linkItems = linkItems.Concat(new[] { new LinkItem(null, MoreText, moreLinks) });
			}

			return linkItems.ToList();
		}


		private LinkItem GetLinkItem(
			IGrouping<string, Link> linksGroup, Func<Link, Node> endPoint, int level)
		{
			IEnumerable<LinkItem> subLinks = GetLinkItems(linksGroup, endPoint, level + 1);

			LinkItem item = new LinkItem(null, GetGroupText(linksGroup.Key), subLinks);

			return item;
		}


		private static string GetGroupText(string key)
		{
			return key.Replace("$", "").Replace("?", "").Replace("*", ".");
		}


		string GetGroupKey(Node node, int level)
		{
			string[] parts = node.Name.FullName.Split(".".ToCharArray());
			return string.Join(".", parts.Take(level));
		}

		private class Layout
		{
			public int Count { get; }
			public Size Size { get; }
			public int RowLength { get; }
			public int Padding { get; }
			public double ScaleFactor { get; }


			public Layout(int count, Size size, int rowLength, int padding, double scaleFactor)
			{
				Count = count;
				Size = size;
				RowLength = rowLength;
				Padding = padding;
				ScaleFactor = scaleFactor;
			}
		}
	}
}
