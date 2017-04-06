using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Dependiator.Modeling.Analyzing;
using Dependiator.Utils;
using Dependiator.Utils.UI;


namespace Dependiator.Modeling
{
	internal class LinkSegment
	{
		private readonly List<Link> nodeLinks = new List<Link>();

		private LinkSegmentViewModel viewModel;

		public LinkSegment(
			Node source,
			Node target,
			Node owner)
		{
			Owner = owner;
			Source = source;
			Target = target;

			viewModel = new LinkSegmentViewModel(this);
		}


		public Rect ItemBounds { get; private set; }
		public double LinkScale => Owner.ItemsCanvasScale;

		public IReadOnlyList<Link> NodeLinks => nodeLinks;

		public Node Source { get; }

		public Node Target { get; }

		public Node Owner { get; }

		public Brush LinkBrush => Source.GetNodeBrush();

		public double X1 { get; private set; }
		public double Y1 { get; private set; }
		public double X2 { get; private set; }
		public double Y2 { get; private set; }


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



		public double LineThickness
		{
			get
			{
				double scale = (Owner.ItemsCanvasScale * 7).MM(0.1, 1);
				double thickness = 0;

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
		}


		

		public void UpdateLinkLine()
		{
			// Assume link nodes are siblings, 
			Rect sourceBounds = Source.ItemBounds;
			Rect targetBounds = Target.ItemBounds;
			double x1 = sourceBounds.X + sourceBounds.Width / 2;
			double y1 = sourceBounds.Y + sourceBounds.Height;
			double x2 = targetBounds.X + targetBounds.Width / 2;
			double y2 = targetBounds.Y;

			if (Source.ParentNode == Target)
			{
				// To parent (out of node to some external node)
				y2 = targetBounds.Y + targetBounds.Height;
			}
			else if (Source == Target.ParentNode)
			{
				// To child (into node to child node)
				y1 = sourceBounds.Y;
			}

			// Line bounds
			double x = Math.Min(x1, x2);
			double y = Math.Min(y1, y2);
			double width = Math.Abs(x2 - x1);
			double height = Math.Abs(y2 - y1);
			double margin = 1;
			ItemBounds = new Rect(x, y, Math.Max(width, margin), Math.Max(height, margin));


			// Line drawing within the bounds
			X1 = 0;
			Y1 = 0;
			X2 = width;
			Y2 = height;

			if (x1 <= x2 && y1 > y2 || x1 > x2 && y1 <= y2)
			{
				Y1 = height;
				Y2 = 0;
			}
		}

		public override string ToString() => $"{Source} -> {Target}";
	}
}