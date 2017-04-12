using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;


namespace Dependiator.Modeling
{
	internal class LinkSegment
	{
		private readonly List<Link> nodeLinks = new List<Link>();
		private Rect itemBounds;
		
		private bool isUpdated = false;

		public LinkSegment(
			Node source,
			Node target,
			Node owner)
		{
			Owner = owner;
			Source = source;
			Target = target;

			ViewModel = new LinkSegmentViewModel(this);
		}


		public LinkSegmentViewModel ViewModel { get; }


		public Rect GetItemBounds()
		{
			if (!isUpdated)
			{
				isUpdated = true;

				if (Source.NodeBounds == Rect.Empty || Target.NodeBounds == Rect.Empty)
				{
					return Rect.Empty;
				}

				UpdateLine(this);
			}

			return itemBounds;
		}

		public Point L1 { get; private set; }
		public Point L2 { get; private set; }



		public double LinkScale => Owner.ItemsScale;

		public IReadOnlyList<Link> NodeLinks => nodeLinks;

		public Node Source { get; }

		public Node Target { get; }

		public Node Owner { get; }

		public Brush LinkBrush => Source == Target.ParentNode
			? Target.GetNodeBrush()
			: Source.GetNodeBrush();



		public double LineThickness => GetLineThickness();


		public string ToolTip
		{
			get
			{
				string tip = $"{this},  {NodeLinks.Count} references:";
				int maxLinks = 40;
				foreach (Link reference in NodeLinks.Take(maxLinks))
				{
					tip += $"\n  {reference}";
				}

				if (NodeLinks.Count > maxLinks)
				{
					tip += "\n  ...";
				}

				return tip;
			}
		}


		public void Add(Link link)
		{
			if (nodeLinks.Any(l => l.Source == link.Source && l.Target == link.Target))
			{
				return;
			}

			nodeLinks.Add(link);
		}


		public bool CanBeShown()
		{
			return (Source.CanShowNode() && Target.CanShowNode());
		}


		public void UpdateVisibility()
		{
			isUpdated = false;
			if (CanBeShown())
			{
				//if (!ViewModel.CanShow)
				{
					ViewModel.Show();
					ViewModel.NotifyAll();
				}
			}
			else
			{
				if (ViewModel.CanShow)
				{
					ViewModel.Hide();
					ViewModel.NotifyAll();
				}
			}
		}


		private double GetLineThickness()
		{
			double scale = (Owner.ItemsScale).MM(0.1, 0.7);
			double thickness;

			if (NodeLinks.Count < 5)
			{
				thickness = 1;
			}
			else if (NodeLinks.Count < 15)
			{
				thickness = 2;
			}
			else
			{
				thickness = 3;
			}

			return thickness * scale;
		}


		private static void UpdateLine(LinkSegment segment)
		{
			Node source = segment.Source;
			Node target = segment.Target;
			Rect sourceBounds = source.NodeBounds;
			Rect targetBounds = target.NodeBounds;

			// We start by assuming source and target nodes are siblings, 
			// I.e. line starts at source middle bottom and ends at target middle top
			double x1 = sourceBounds.X + sourceBounds.Width / 2;
			double y1 = sourceBounds.Y + sourceBounds.Height;
			double x2 = targetBounds.X + targetBounds.Width / 2;
			double y2 = targetBounds.Y;

			if (source.ParentNode == target)
			{
				// The target is a parent of the source, i.e. line ends at the bottom of the target node
				x2 = (targetBounds.Width / 2) * target.ItemsScaleFactor
							+ target.ItemsOffset.X / target.ItemsScale;
				y2 = (targetBounds.Height) * target.ItemsScaleFactor
					+ (target.ItemsOffset.Y - 28) / target.ItemsScale;

			}
			else if (source == target.ParentNode)
			{
				// The target is the child of the source, i.e. line start at the top of the source
				x1 = (sourceBounds.Width / 2) * source.ItemsScaleFactor
					+ source.ItemsOffset.X / source.ItemsScale;
				y1 = source.ItemsOffset.Y / source.ItemsScale;
			}

			// Line bounds:
			double x = Math.Min(x1, x2);
			double y = Math.Min(y1, y2);
			double width = Math.Abs(x2 - x1);
			double height = Math.Abs(y2 - y1);

			// Ensure the rect is at least big enough to contain the width of the line
			width = Math.Max(width, segment.LineThickness + 1);
			height = Math.Max(height, segment.LineThickness + 1);

			Rect lineBounds = new Rect(x, y, width, height);

			// Line drawing within the bounds
			double XX1 = 0;
			double XY1 = 0;
			double XX2 = width;
			double XY2 = height;

			if (x1 <= x2 && y1 > y2 || x1 > x2 && y1 <= y2)
			{
				// Need to flip the line
				XY1 = height;
				XY2 = 0;
			}

			Point l1 = new Point(XX1, XY1);
			Point l2 = new Point(XX2, XY2);

			segment.SetBounds(lineBounds, l1, l2);
		}


		private void SetBounds(Rect lineBounds, Point l1, Point l2)
		{
			itemBounds = lineBounds;
			L1 = l1;
			L2 = l2;
		}


		public override string ToString() => $"{Source} -> {Target}";
	}
}