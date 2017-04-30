using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using Dependiator.Modeling.Items;


namespace Dependiator.Modeling.Links
{
	internal class LinkLineViewModel : ItemViewModel
	{
		private readonly ILinkService linkService;
		private readonly LinkLine linkLine;

		public LinkLineViewModel(
			ILinkService linkService,
			LinkLine linkLine)
		{
			this.linkService = linkService;
			this.linkLine = linkLine;
		}

		protected override Rect GetItemBounds() => linkLine.GetItemBounds();


		public double X1 => linkLine.L1.X * linkLine.ItemsScale;
		public double Y1 => linkLine.L1.Y * linkLine.ItemsScale;
		public double X2 => linkLine.L2.X * linkLine.ItemsScale;
		public double Y2 => linkLine.L2.Y * linkLine.ItemsScale;
		public double StrokeThickness => linkService.GetLineThickness(linkLine);

		
		public Brush LineBrush => linkLine.IsEmpty ? Brushes.DimGray :
			linkLine.Source == linkLine.Target.ParentNode
				? linkLine.Target.GetNodeBrush()
				: linkLine.Source.GetNodeBrush();

		public Brush HoverBrush => Brushes.Transparent;
		public string StrokeDash => linkLine.IsEmpty ? "2,2" : "";
		public string ToolTip => GetToolTip();

		public override string ToString() => linkLine.ToString();


		public void ToggleLine() => linkLine.ToggleLine();


		private string GetToolTip()
		{
			IReadOnlyList<LinkGroup> linkGroups = linkService.GetLinkGroups(linkLine);
			string tip = "";

			foreach (var group in linkGroups)
			{
				tip += $"\n  {group.Source} -> {group.Target} ({group.Links.Count})";
			}

			int linksCount = linkLine.Links.Count;
			tip = $"{this} {linksCount} links:" + tip;

			return tip;
		}
	}
}