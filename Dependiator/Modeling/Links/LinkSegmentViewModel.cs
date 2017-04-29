using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using Dependiator.Modeling.Items;


namespace Dependiator.Modeling.Links
{
	internal class LinkSegmentViewModel : ItemViewModel
	{
		private readonly ILinkItemService linkItemService;
		private readonly LinkSegment linkSegment;

		public LinkSegmentViewModel(
			ILinkItemService linkItemService,
			LinkSegment linkSegment)
		{
			this.linkItemService = linkItemService;
			this.linkSegment = linkSegment;
		}

		protected override Rect GetItemBounds() => linkSegment.GetItemBounds();


		public double X1 => linkSegment.L1.X * linkSegment.ItemsScale;
		public double Y1 => linkSegment.L1.Y * linkSegment.ItemsScale;
		public double X2 => linkSegment.L2.X * linkSegment.ItemsScale;
		public double Y2 => linkSegment.L2.Y * linkSegment.ItemsScale;
		public double StrokeThickness => GetLineThickness();

		
		public Brush LineBrush => linkSegment.IsEmpty ? Brushes.DimGray :
			linkSegment.Source == linkSegment.Target.ParentNode
				? linkSegment.Target.GetNodeBrush()
				: linkSegment.Source.GetNodeBrush();

		public Brush HoverBrush => Brushes.Transparent;
		public string StrokeDash { get; set; } = "";
		public string ToolTip => GetToolTip();

		public override string ToString() => linkSegment.ToString();


		public void ToggleLine() => linkSegment.ToggleLine();


		private string GetToolTip()
		{
			IReadOnlyList<LinkGroup> linkGroups = linkItemService.GetLinkGroups(linkSegment);
			string tip = "";

			foreach (var group in linkGroups)
			{
				tip += $"\n  {group.Source} -> {group.Target} ({group.Links.Count})";
			}

			int linksCount = linkSegment.NodeLinks.Count;
			tip = $"{this} {linksCount} links, splits into {linkGroups.Count} links:" + tip;

			//int maxLinks = 40;
			//tip += $"\n";

			//foreach (Link reference in NodeLinks.Take(maxLinks))
			//{
			//	tip += $"\n  {reference}";
			//}

			//if (NodeLinks.Count > maxLinks)
			//{
			//	tip += "\n  ...";
			//}


			return tip;
		}

		private double GetLineThickness()
		{
			double scale = (linkSegment.Owner.ItemsScale).MM(0.1, 0.7);
			double thickness;

			if (linkSegment.NodeLinks.Count < 5)
			{
				thickness = 1;
			}
			else if (linkSegment.NodeLinks.Count < 15)
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
}