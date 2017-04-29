using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using Dependiator.Modeling.Items;


namespace Dependiator.Modeling.Links
{
	internal class LinkSegmentViewModel : ItemViewModel
	{
		private readonly ILinkService linkService;
		private readonly LinkSegment linkSegment;

		public LinkSegmentViewModel(
			ILinkService linkService,
			LinkSegment linkSegment)
		{
			this.linkService = linkService;
			this.linkSegment = linkSegment;
		}

		protected override Rect GetItemBounds() => linkSegment.GetItemBounds();


		public double X1 => linkSegment.L1.X * linkSegment.ItemsScale;
		public double Y1 => linkSegment.L1.Y * linkSegment.ItemsScale;
		public double X2 => linkSegment.L2.X * linkSegment.ItemsScale;
		public double Y2 => linkSegment.L2.Y * linkSegment.ItemsScale;
		public double StrokeThickness => linkService.GetLineThickness(linkSegment);

		
		public Brush LineBrush => linkSegment.IsEmpty ? Brushes.DimGray :
			linkSegment.Source == linkSegment.Target.ParentNode
				? linkSegment.Target.GetNodeBrush()
				: linkSegment.Source.GetNodeBrush();

		public Brush HoverBrush => Brushes.Transparent;
		public string StrokeDash => linkSegment.IsEmpty ? "2,2" : "";
		public string ToolTip => GetToolTip();

		public override string ToString() => linkSegment.ToString();


		public void ToggleLine() => linkSegment.ToggleLine();


		private string GetToolTip()
		{
			IReadOnlyList<LinkGroup> linkGroups = linkService.GetLinkGroups(linkSegment);
			string tip = "";

			foreach (var group in linkGroups)
			{
				tip += $"\n  {group.Source} -> {group.Target} ({group.Links.Count})";
			}

			int linksCount = linkSegment.NodeLinks.Count;
			tip = $"{this} {linksCount} links:" + tip;

			return tip;
		}
	}
}