using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.ModelViewing.ModelHandling.Private.Items;


namespace Dependinator.ModelViewing.Nodes.Private
{
	internal class NodeLayoutService : INodeLayoutService
	{
		private static readonly Size DefaultSize = new Size(200, 100);

		private readonly Layout[] layouts =
		{
			new Layout(1, 1, 50, 5, 40, 20),
			new Layout(2, 1, 50, 2, 125, 50),
			new Layout(4, 2, 50, 2, 125, 50),
			new Layout(12, 4, 100, 1, 150, 120),
			new Layout(int.MaxValue, 6, 50, 0.8, 150, 120),
		};



		public void SetLayout(NodeViewModel nodeViewMode)
		{
			if (!nodeViewMode.Node.Bounds.Same(RectEx.Zero))
			{
				nodeViewMode.ItemBounds = nodeViewMode.Node.Bounds;
				return;
			}

			if (nodeViewMode.Node.Parent.IsLayoutCompleted)
			{
				AdjustLayout(nodeViewMode);
			}
			else
			{
				ResetLayout(nodeViewMode);
			}
		}


		private void ResetLayout(NodeViewModel nodeViewMode)
		{
			Node parent = nodeViewMode.Node.Parent;

			Layout layout = GetLayout(parent);

			SetScale(layout, parent);

			int index = 0;
			IReadOnlyList<Node> sortedChildren = parent.Children;
			//	.OrderBy(child => child, NodeComparer.Comparer(parent)).ToList();

			foreach (Node child in sortedChildren)
			{
				Rect bounds = GetBounds(index++, layout);

				child.ViewModel.ItemBounds = bounds;
			}
		}


		private void AdjustLayout(NodeViewModel nodeViewMode)
		{
			Node parent = nodeViewMode.Node.Parent;

			Layout layout = GetLayout(parent);

			SetScale(layout, parent);

			int index = 0;

			while (true)
			{
				Rect bounds = GetBounds(index++, layout);

				if (!IsIntersecting(bounds, parent.Children))
				{
					nodeViewMode.ItemBounds = bounds;
					break;
				}
			}
		}



		private static void SetScale(Layout layout, Node parentNode)
		{
			double scaleFactor = layout.ScaleFactor;

			if (!scaleFactor.Same(parentNode.ItemsCanvas.ScaleFactor))
			{
				parentNode.ItemsCanvas.SetScaleFactor(scaleFactor);
				parentNode.ItemsCanvas.UpdateScale();
			}
		}


		private static bool IsIntersecting(Rect bounds, IEnumerable<Node> children)
		{
			return children.Any(child => child.ViewModel.ItemBounds.IntersectsWith(bounds));
		}


		private Layout GetLayout(Node parent)
		{
			if (parent.IsRoot)
			{
				return layouts[1];
			}

			int itemCount = parent.Children.Count;

			return layouts.First(l => itemCount <= l.MaxItems);
		}


		private static Rect GetBounds(int siblingIndex, Layout layout)
		{
			Size size = DefaultSize;
			double x = (siblingIndex % layout.RowLength) * (size.Width + layout.Padding) + layout.XMargin;
			double y = (siblingIndex / layout.RowLength) * (size.Height + layout.Padding) + layout.YMargin;

			Point location = new Point(x, y);

			Rect newBounds = new Rect(location, size);
			return newBounds;
		}


		private static bool IsParentShowing(NodeViewModel nodeViewMode)
		{
			return nodeViewMode.Node.IsRoot
				|| nodeViewMode.Node.Parent.ViewModel.IsShowing;
		}




		private class Layout
		{
			public int MaxItems { get; }
			public int RowLength { get; }
			public int Padding { get; }
			public double ScaleFactor { get; }
			public double XMargin { get; }
			public double YMargin { get; }


			public Layout(
				int maxItems,
				int rowLength,
				int padding,
				double relativeScaleFactor,
				double xMargin,
				double yMargin)
			{
				MaxItems = maxItems;
				RowLength = rowLength;
				Padding = padding;
				ScaleFactor = relativeScaleFactor * ItemsCanvas.DefaultScaleFactor;
				XMargin = xMargin;
				YMargin = yMargin;
			}
		}
	}
}