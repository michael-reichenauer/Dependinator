using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using Dependinator.ModelViewing.Links.Private;
using Dependinator.ModelViewing.Private.Items;


namespace Dependinator.ModelViewing.Links
{
	internal class LinkLineViewModel : ItemViewModel
	{
		private readonly ILinkService linkService;
		private readonly LinkLineOld linkLine;
		
		public LinkLineViewModel(
			ILinkService linkService,
			LinkLineOld linkLine)
		{
			this.linkService = linkService;
			this.linkLine = linkLine;
		}

		protected Rect GetItemBounds() => linkLine.GetItemBounds();


		public double X1 => linkLine.L1.X * linkLine.ItemsScale;
		public double Y1 => linkLine.L1.Y * linkLine.ItemsScale;
		public double X2 => linkLine.L2.X * linkLine.ItemsScale;
		public double Y2 => linkLine.L2.Y * linkLine.ItemsScale;
		public double StrokeThickness =>
			IsMouseOver ? 
			linkService.GetLineThickness(linkLine) * 2: 
			linkService.GetLineThickness(linkLine);

		
		public Brush LineBrush => GetLineBrush();

		public Brush LineBackgroundBrush => Brushes.Transparent;


		public bool IsMouseOver { get; private set; }


		private Brush GetLineBrush()
		{
			if (IsMouseOver)
			{
				return linkLine.Source == linkLine.Target.ParentNode
					? linkLine.Target.GetHighlightNodeBrush()
					: linkLine.Source.GetHighlightNodeBrush();
			}

			if (linkLine.IsEmpty)
			{
				return Brushes.DimGray;
			}

			return linkLine.Source == linkLine.Target.ParentNode
				? linkLine.Target.GetNodeBrush()
				: linkLine.Source.GetNodeBrush();
		}


		public Brush HoverBrush => Brushes.GhostWhite;

		public string StrokeDash => linkLine.HasHidden ? "2,2" : "";
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
			tip = $"{linksCount} links:" + tip;

			//if (linkLine.Source == linkLine.Target.ParentNode
			//    || linkLine.Target == linkLine.Source.ParentNode)
			//{
			//	tip = $"{linksCount} links:" + tip;
			//}
			//else
			//{
			//	tip = $"{this} {linksCount} links:" + tip;
			//}

			return tip;
		}


		public void ZoomLinks(double zoom, Point viewPosition)
		{
			linkLine.ZoomLinks(zoom, viewPosition);
		}


		public void OnMouseEnter()
		{
			IsMouseOver = true;
			Notify(nameof(LineBrush), nameof(StrokeThickness));
		}


		public void OnMouseLeave()
		{
			IsMouseOver = false;
			Notify(nameof(LineBrush), nameof(StrokeThickness));
		}
	}


	//internal class LinkLineViewModel : ItemViewModel
	//{
	//	private readonly ILinkService linkService;
	//	private readonly LinkLineOld linkLine;

	//	public LinkLineViewModel(
	//		ILinkService linkService,
	//		LinkLineOld linkLine)
	//	{
	//		this.linkService = linkService;
	//		this.linkLine = linkLine;
	//	}

	//	protected Rect GetItemBounds() => linkLine.GetItemBounds();


	//	public double X1 => linkLine.L1.X * linkLine.ItemsScale;
	//	public double Y1 => linkLine.L1.Y * linkLine.ItemsScale;
	//	public double X2 => linkLine.L2.X * linkLine.ItemsScale;
	//	public double Y2 => linkLine.L2.Y * linkLine.ItemsScale;
	//	public double StrokeThickness =>
	//		IsMouseOver ?
	//			linkService.GetLineThickness(linkLine) * 2 :
	//			linkService.GetLineThickness(linkLine);


	//	public Brush LineBrush => GetLineBrush();

	//	public Brush LineBackgroundBrush => Brushes.Transparent;


	//	public bool IsMouseOver { get; private set; }


	//	private Brush GetLineBrush()
	//	{
	//		if (IsMouseOver)
	//		{
	//			return linkLine.Source == linkLine.Target.ParentNode
	//				? linkLine.Target.GetHighlightNodeBrush()
	//				: linkLine.Source.GetHighlightNodeBrush();
	//		}

	//		if (linkLine.IsEmpty)
	//		{
	//			return Brushes.DimGray;
	//		}

	//		return linkLine.Source == linkLine.Target.ParentNode
	//			? linkLine.Target.GetNodeBrush()
	//			: linkLine.Source.GetNodeBrush();
	//	}


	//	public Brush HoverBrush => Brushes.GhostWhite;

	//	public string StrokeDash => linkLine.HasHidden ? "2,2" : "";
	//	public string ToolTip => GetToolTip();

	//	public override string ToString() => linkLine.ToString();


	//	public void ToggleLine() => linkLine.ToggleLine();


	//	private string GetToolTip()
	//	{
	//		IReadOnlyList<LinkGroup> linkGroups = linkService.GetLinkGroups(linkLine);
	//		string tip = "";

	//		foreach (var group in linkGroups)
	//		{
	//			tip += $"\n  {group.Source} -> {group.Target} ({group.Links.Count})";
	//		}

	//		int linksCount = linkLine.Links.Count;
	//		tip = $"{linksCount} links:" + tip;

	//		//if (linkLine.Source == linkLine.Target.ParentNode
	//		//    || linkLine.Target == linkLine.Source.ParentNode)
	//		//{
	//		//	tip = $"{linksCount} links:" + tip;
	//		//}
	//		//else
	//		//{
	//		//	tip = $"{this} {linksCount} links:" + tip;
	//		//}

	//		return tip;
	//	}


	//	public void ZoomLinks(double zoom, Point viewPosition)
	//	{
	//		linkLine.ZoomLinks(zoom, viewPosition);
	//	}


	//	public void OnMouseEnter()
	//	{
	//		IsMouseOver = true;
	//		Notify(nameof(LineBrush), nameof(StrokeThickness));
	//	}


	//	public void OnMouseLeave()
	//	{
	//		IsMouseOver = false;
	//		Notify(nameof(LineBrush), nameof(StrokeThickness));
	//	}
	//}

}