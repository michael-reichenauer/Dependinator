//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Windows;
//using System.Windows.Media;
//using Dependiator.Modeling.Analyzing;
//using Dependiator.Utils;
//using Dependiator.Utils.UI;


//namespace Dependiator.Modeling
//{
//	internal class Link : Item
//	{
//		private readonly List<NodeLink> nodeLinks = new List<NodeLink>();


//		public Link(
//			IItemService itemService,
//			Node source,
//			Node target)
//			: base(itemService, source)
//		{
//			Source = source;
//			Target = target;

//			ViewModel = new LinkViewModel(this);
//		}


//		public override ViewModel ViewModel { get; }

//		public IReadOnlyList<NodeLink> NodeLinks => nodeLinks;

//		public Node Source { get; }

//		public Node Target { get; }

//		public Brush LinkBrush => Source.RectangleBrush;

//		public double X1 { get; private set; }
//		public double Y1 { get; private set; }
//		public double X2 { get; private set; }
//		public double Y2 { get; private set; }

//		public string ToolTip
//		{
//			get
//			{
//				string tip = $"{this},  {NodeLinks.Count} references:";
//				int maxLinks = 40;
//				foreach (NodeLink reference in NodeLinks.Take(maxLinks))
//				{
//					tip += $"\n  {reference}";
//				}

//				if (NodeLinks.Count > maxLinks)
//				{
//					tip += "\n  ...";
//				}

//				return tip;
//			}
//		}

//		public void Add(NodeLink nodeLink)
//		{
//			if (nodeLinks.Any(l => l.Source == nodeLink.Source && l.Target == nodeLink.Target))
//			{
//				return;
//			}

//			nodeLinks.Add(nodeLink);
//		}


//		public override bool CanBeShown()
//		{
//			return ItemScale > 0.03 && (Source.CanBeShown() || Target.CanBeShown());
//		}


//		public override void ItemRealized()
//		{
//			base.ItemRealized();
//		}


//		public override void ItemVirtualized()
//		{
//			base.ItemVirtualized();			
//		}


//		public double LineThickness
//		{
//			get
//			{
//				double scale = (ItemScale * 7).MM(0.1, 1);
//				double thickness = 0;

//				if (NodeLinks.Count < 5)
//				{
//					thickness = 1;
//				}
//				else if (NodeLinks.Count < 15)
//				{
//					thickness = 2;
//				}
//				else
//				{
//					thickness = 3;
//				}

//				return thickness * scale;
//			}
//		}


//		//public void UpdateLinkLine()
//		//{
//		//	// Assume link nodes are siblings, 
//		//	double x1 = Source.ItemBounds.X + Source.ItemBounds.Width / 2;
//		//	double y1 = Source.ItemBounds.Y + Source.ItemBounds.Height;
//		//	double x2 = Target.ItemBounds.X + Target.ItemBounds.Width / 2;
//		//	double y2 = Target.ItemBounds.Y;

//		//	if (Source.ParentNode == Target)
//		//	{
//		//		// To parent (out of node to some external node)
//		//		y2 = Target.ItemCanvasBounds.Y + Target.ItemCanvasBounds.Height;
//		//	}
//		//	else if (Source == Target.ParentNode)
//		//	{
//		//		// To child (into node to child node)
//		//		y1 = Source.ItemCanvasBounds.Y;
//		//	}

//		//	// Line bounds
//		//	double x = Math.Min(x1, x2);
//		//	double y = Math.Min(y1, y2);
//		//	double width = Math.Abs(x2 - x1);
//		//	double height = Math.Abs(y2 - y1);
//		//	double margin = 1;
//		//	ItemBounds = new Rect(x, y, Math.Max(width, margin), Math.Max(height, margin));
//		//	//Rect itemCanvasBounds = ItemCanvasBounds;

//		//	//Rect childItemBounds = ItemCanvasBounds;
//		//	//double childItemScale = 1 / ItemScaleFactor;
//		//	//childItemBounds.Scale(childItemScale, childItemScale);

//		//	//Rect itemBounds = new Rect(
//		//	//	x,
//		//	//	y,
//		//	//	childItemBounds.Width,
//		//	//	childItemBounds.Height);

//		//	////Rect newRect = ParentItem.GetChildItemCanvasBounds(itemBounds);

//		//	//ItemBounds = itemBounds;


//		//	// Line drawing within the bounds
//		//	X1 = 0;
//		//	Y1 = 0;
//		//	X2 = width;
//		//	Y2 = height;

//		//	if (x1 <= x2 && y1 > y2 || x1 > x2 && y1 <= y2)
//		//	{
//		//		Y1 = height;
//		//		Y2 = 0;
//		//	}
//		//}

//		public void UpdateLinkLine()
//		{
//			// Assume link nodes are siblings, 
//			double x1 = Source.ItemCanvasBounds.X + Source.ItemCanvasBounds.Width / 2;
//			double y1 = Source.ItemCanvasBounds.Y + Source.ItemCanvasBounds.Height;
//			double x2 = Target.ItemCanvasBounds.X + Target.ItemCanvasBounds.Width / 2;
//			double y2 = Target.ItemCanvasBounds.Y;

//			if (Source.ParentNode == Target)
//			{
//				// To parent (out of node to some external node)
//				y2 = Target.ItemCanvasBounds.Y + Target.ItemCanvasBounds.Height;
//			}
//			else if (Source == Target.ParentNode)
//			{
//				// To child (into node to child node)
//				y1 = Source.ItemCanvasBounds.Y;
//			}

//			// Line bounds
//			double x = Math.Min(x1, x2);
//			double y = Math.Min(y1, y2);
//			double width = Math.Abs(x2 - x1);
//			double height = Math.Abs(y2 - y1);
//			double margin = 1;
//			ItemCanvasBounds = new Rect(x, y, Math.Max(width, margin), Math.Max(height, margin));
		

//			// Line drawing within the bounds
//			X1 = 0;
//			Y1 = 0;
//			X2 = width;
//			Y2 = height;

//			if (x1 <= x2 && y1 > y2 || x1 > x2 && y1 <= y2)
//			{
//				Y1 = height;
//				Y2 = 0;
//			}
//		}

//		public override string ToString() => $"{Source} -> {Target}";
//	}
//}