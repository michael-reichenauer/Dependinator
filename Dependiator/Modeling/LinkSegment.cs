using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Windows;
using System.Windows.Media;
using System.Xml.Linq;
using Dependiator.Modeling.Analyzing;
using Dependiator.Utils;
using Dependiator.Utils.UI;


namespace Dependiator.Modeling
{
	internal class LinkSegment
	{
		private readonly List<Link> nodeLinks = new List<Link>();
		private Rect itemBounds;
		private Rect sourceBounds;
		private Rect targetBounds;

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
			if (Source.ItemBounds == Rect.Empty || Target.ItemBounds == Rect.Empty)
			{
				return Rect.Empty;
			}

			if (sourceBounds != Source.ItemBounds || targetBounds != Target.ItemBounds)
			{
				// Source or target has moved, lets upate values
				sourceBounds = Source.ItemBounds;
				targetBounds = Target.ItemBounds;

				UpdateLine();

			}

			return itemBounds;
		}


		public double X1 { get; private set; }
		public double Y1 { get; private set; }
		public double X2 { get; private set; }
		public double Y2 { get; private set; }



		public double LinkScale => Owner.ItemsCanvasScale;

		public IReadOnlyList<Link> NodeLinks => nodeLinks;

		public Node Source { get; }

		public Node Target { get; }

		public Node Owner { get; }

		public Brush LinkBrush => Source.GetNodeBrush();

		

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
			return LinkScale > 0.3 && (Source.CanShowNode() || Target.CanShowNode());
		}


		public void UpdateVisability()
		{
			if (CanBeShown())
			{
				ViewModel.Show();
				ViewModel.NotifyAll();
			}
			else
			{
				ViewModel.Hide();
				ViewModel.NotifyAll();
			}
		}


		private double GetLineThickness()
		{
			double scale = (Owner.ItemsCanvasScale / 2.5).MM(0.1, 1);
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


		private void UpdateLine()
		{
			// We start by assuming source and target nodes are siblings, 
			// I.e. line starts at source middle bottom and ends at target middle top
			Rect sourceBounds = Source.ItemBounds;
			Rect targetBounds = Target.ItemBounds;
			double x1 = sourceBounds.X + sourceBounds.Width / 2;
			double y1 = sourceBounds.Y + sourceBounds.Height;
			double x2 = targetBounds.X + targetBounds.Width / 2;
			double y2 = targetBounds.Y;

			if (Source.ParentNode == Target)
			{
				// The target is a parent of the source, i.e. line ends at the botom of the target node
				y2 = targetBounds.Y + targetBounds.Height;

				Rect targetViewbox = Target.ActualViewbox;
				if (targetViewbox != Rect.Empty)
				{
					x2 = targetViewbox.X + (targetViewbox.Width / 2);
					y2 = targetViewbox.Y + targetViewbox.Height;
				}
			}
			else if (Source == Target.ParentNode)
			{
				// The target is the child of the source, i.e. line start at the top of the source
				y1 = 0;
				Rect sourceViewBox = Source.ActualViewbox;
				if (sourceViewBox != Rect.Empty)
				{
					x1 = sourceViewBox.X + (sourceViewBox.Width / 2);
					y1 = sourceViewBox.Y;
				}
			}

			// Line rect bounds:
			double x = Math.Min(x1, x2);
			double y = Math.Min(y1, y2);
			double width = Math.Abs(x2 - x1);
			double height = Math.Abs(y2 - y1);

			// Ensure the rect is at list big enaought to contain the width of the line
			width = Math.Max(width, LineThickness + 1);
			height = Math.Max(height, LineThickness + 1);

			itemBounds = new Rect(x, y, width, height);

			// Line drawing within the bounds
			X1 = 0;
			Y1 = 0;
			X2 = width;
			Y2 = height;

			if (x1 <= x2 && y1 > y2 || x1 > x2 && y1 <= y2)
			{
				// Need to flip the line
				Y1 = height;
				Y2 = 0;
			}

			Owner.UpdateItem(ViewModel);
		}

		public override string ToString() => $"{Source} -> {Target}";
	}
}